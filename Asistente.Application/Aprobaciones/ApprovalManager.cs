using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Asistente.Application.Interfaces;
using Asistente.Domain.Entities;
using Asistente.Domain.Entities.Aprobaciones;
using Asistente.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Asistente.Application.Aprobaciones;

/// <summary>
/// Approval Manager (ETAPA 19, punto 8 / actividades 1-10).
/// Componente independiente responsable de TODO el ciclo de vida de las aprobaciones:
/// crear solicitudes, asignar aprobadores, validar políticas, esperar decisión, registrar
/// auditoría, delegar y reanudar la ejecución del plan cuando corresponde.
/// Reglas de negocio (punto 12): la IA nunca aprueba sus propias acciones (Regla 6),
/// los comentarios se conservan (Regla 3), la delegación queda auditada (Regla 5).
/// </summary>
public class ApprovalManager
{
    private readonly IApprovalRequestRepository _reqRepo;
    private readonly IApprovalDecisionRepository _decRepo;
    private readonly IApprovalAssigneeRepository _asgRepo;
    private readonly IApprovalPolicyRepository _polRepo;
    private readonly IPlanRepository _planRepo;
    private readonly IPlanExecutionLogRepository _logRepo;
    private readonly ILogger<ApprovalManager> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IUsuarioRepository _usuarioRepo;

    public ApprovalManager(
        IApprovalRequestRepository reqRepo,
        IApprovalDecisionRepository decRepo,
        IApprovalAssigneeRepository asgRepo,
        IApprovalPolicyRepository polRepo,
        IPlanRepository planRepo,
        IPlanExecutionLogRepository logRepo,
        ILogger<ApprovalManager> logger,
        IServiceScopeFactory scopeFactory,
        IUsuarioRepository usuarioRepo)
    {
        _reqRepo = reqRepo;
        _decRepo = decRepo;
        _asgRepo = asgRepo;
        _polRepo = polRepo;
        _planRepo = planRepo;
        _logRepo = logRepo;
        _logger = logger;
        _scopeFactory = scopeFactory;
        _usuarioRepo = usuarioRepo;
    }

    /// <summary>
    /// Roles habilitados para aprobar planes. El rol "Usuario" solo tiene acceso al
    /// chat y a subir documentos (sin Centro de Aprobaciones), por lo que nunca puede
    /// ser asignado como aprobador ni decidir solicitudes.
    /// </summary>
    private static readonly string[] RolesAprobadores = ["Administrador", "Operador", "Supervisor"];

    private static bool TieneRolAprobador(Usuario usuario) =>
        usuario.Activo && usuario.UsuarioRoles
            .Any(ur => ur.Rol != null && RolesAprobadores.Contains(ur.Rol.Nombre));

    private async Task<Usuario?> ObtenerAprobadorValidoAsync(int idUsuario, CancellationToken ct)
    {
        var usuario = await _usuarioRepo.GetByIdAsync(idUsuario);
        return usuario != null && TieneRolAprobador(usuario) ? usuario : null;
    }

    /// <summary>
    /// Crea una solicitud de aprobación para un paso sensible de un plan y la deja en espera.
    /// (Actividades 1, 3, 4, 5). Asigna aprobadores según la política activa.
    /// </summary>
    public async Task<ApprovalRequest> CrearSolicitudAsync(
        int idPlan, TipoAprobacion tipo, int solicitante, string observaciones,
        List<int> aprobadores, int? idPolicy = null, CancellationToken ct = default)
    {
        var policy = idPolicy.HasValue
            ? await _polRepo.GetByIdAsync(idPolicy.Value, ct)
            : await _polRepo.GetActivaAsync(ct);

        var solicitud = new ApprovalRequest
        {
            Codigo = "APROV-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss"),
            IdPlan = idPlan,
            Tipo = tipo,
            Estado = EstadoAprobacion.Pendiente,
            FechaSolicitud = DateTime.UtcNow,
            Solicitante = solicitante,
            Observaciones = observaciones,
            IdPolicy = policy?.IdPolicy
        };

        if (policy != null && policy.TiempoMaximoHoras > 0)
            solicitud.FechaVencimiento = DateTime.UtcNow.AddHours(policy.TiempoMaximoHoras);

        solicitud = await _reqRepo.AddAsync(solicitud, ct);

        // Asignar aprobadores (actividad 4 y 5). El primero es el principal.
        // Solo Administrador/Operador/Supervisor pueden aprobar: se descartan los
        // usuarios con rol Usuario (sin acceso al Centro de Aprobaciones).
        var aprobadoresValidos = new List<int>();
        foreach (var idUsuario in aprobadores.Distinct())
        {
            if (await ObtenerAprobadorValidoAsync(idUsuario, ct) != null)
                aprobadoresValidos.Add(idUsuario);
            else
                _logger.LogWarning("Usuario {Id} descartado como aprobador: sin rol aprobador o inactivo.", idUsuario);
        }

        if (aprobadoresValidos.Count == 0)
            throw new InvalidOperationException(
                "Ningún aprobador válido: solo usuarios con rol Administrador, Operador o Supervisor pueden aprobar.");

        int idx = 0;
        foreach (var idUsuario in aprobadoresValidos)
        {
            await _asgRepo.AddAsync(new ApprovalAssignee
            {
                IdApproval = solicitud.IdApproval,
                IdUsuario = idUsuario,
                EsPrincipal = idx == 0,
                Estado = "Pendiente"
            }, ct);
            idx++;
        }

        await RegistrarAuditoriaAsync(idPlan, solicitud.IdApproval, "SolicitudAprobacion",
            $"Creada {solicitud.Codigo} ({tipo}) para el plan #{idPlan}. Aprobadores: {aprobadoresValidos.Count}.", ct);

        _logger.LogInformation("Solicitud de aprobación {Codigo} creada para plan {Plan}", solicitud.Codigo, idPlan);
        return solicitud;
    }

    /// <summary>
    /// Bloquea hasta que la solicitud se resuelva (Aprobado/Rechazado/Expirado/Cancelado) o venza
    /// el timeout de espera. Devuelve la solicitud final. Usado por el Planner para "esperar"
    /// dentro del flujo sin reconstruir el plan (RF punto 11).
    /// </summary>
    public async Task<ApprovalRequest> EsperarResolucionAsync(
        int idApproval, TimeSpan timeout, CancellationToken ct = default)
    {
        var fin = DateTime.UtcNow.Add(timeout);
        while (DateTime.UtcNow < fin)
        {
            ct.ThrowIfCancellationRequested();
            var req = await _reqRepo.GetByIdAsync(idApproval, ct);
            if (req == null) throw new InvalidOperationException($"Solicitud {idApproval} no existe.");
            if (req.Estado is EstadoAprobacion.Aprobado or EstadoAprobacion.Rechazado
                or EstadoAprobacion.Expirado or EstadoAprobacion.Cancelado)
                return req;

            // Vencimiento proactivo (actividad 8).
            if (req.FechaVencimiento.HasValue && req.FechaVencimiento < DateTime.UtcNow)
            {
                await ExpirarAsync(idApproval, ct);
                return await _reqRepo.GetByIdAsync(idApproval, ct) ?? req;
            }

            await Task.Delay(2000, ct);
        }
        throw new TimeoutException($"La solicitud {idApproval} no fue resuelta en {timeout.TotalSeconds}s.");
    }

    /// <summary>
    /// Aprueba o rechaza (actividades 6, 9, 10). Valida permisos (Regla 2/7) y políticas
    /// (unanimidad / mayoría). Si se alcanza el criterio, la solicitud pasa a Aprobado/Rechazado
    /// y se reanuda el plan (actividad 9).
    /// </summary>
    public async Task<ApprovalRequest> DecidirAsync(
        int idApproval, int idUsuario, string decision, string? comentario, CancellationToken ct = default)
    {
        var req = await _reqRepo.GetByIdAsync(idApproval, ct)
                  ?? throw new InvalidOperationException($"Solicitud {idApproval} no encontrada.");

        if (req.Estado is EstadoAprobacion.Aprobado or EstadoAprobacion.Rechazado
            or EstadoAprobacion.Expirado or EstadoAprobacion.Cancelado)
            return req; // ya resuelta

        // Regla 7: el usuario debe estar autorizado (asignado y pendiente).
        // Se busca la asignación PENDIENTE: un usuario puede tener varias filas
        // (p.ej. delegó y luego le volvieron a delegar); la primera puede estar
        // en estado "Delegado" y no debe bloquear la decisión actual.
        var asignado = req.Asignados.FirstOrDefault(a => a.IdUsuario == idUsuario && a.Estado == "Pendiente");
        if (asignado == null)
            throw new UnauthorizedAccessException(
                $"El usuario {idUsuario} no está autorizado para decidir la solicitud {idApproval}.");

        // Solo Administrador/Operador/Supervisor pueden decidir (el rol Usuario
        // no tiene acceso al Centro de Aprobaciones).
        if (await ObtenerAprobadorValidoAsync(idUsuario, ct) == null)
            throw new UnauthorizedAccessException(
                $"El usuario {idUsuario} no tiene un rol habilitado para aprobar (se requiere Administrador, Operador o Supervisor).");

        // Regla 6: la IA / solicitante nunca aprueba sus propias acciones.
        // Rechazar siempre está permitido (cancelar propia solicitud).
        if (decision == "Aprobar" && idUsuario == req.Solicitante)
            throw new UnauthorizedAccessException("El solicitante no puede aprobar su propia acción (Regla 6).");

        await _decRepo.AddAsync(new ApprovalDecision
        {
            IdApproval = idApproval,
            IdUsuario = idUsuario,
            Decision = decision,
            Comentario = comentario,
            FechaDecision = DateTime.UtcNow
        }, ct);

        asignado.Estado = decision == "Rechazar" ? "Rechazado" : "Aprobado";
        await _asgRepo.UpdateAsync(asignado, ct);

        await RegistrarAuditoriaAsync(req.IdPlan, idApproval, "DecisionAprobacion",
            $"Usuario {idUsuario} -> {decision}. Comentario: {comentario}", ct);

        // Evaluar política (actividad 5).
        var policy = req.Policy;
        var aprobados = req.Asignados.Count(a => a.Estado == "Aprobado");
        var rechazados = req.Asignados.Count(a => a.Estado == "Rechazado");
        var pendientes = req.Asignados.Count(a => a.Estado == "Pendiente");

        if (decision == "Rechazar")
        {
            req.Estado = EstadoAprobacion.Rechazado;
            await _reqRepo.UpdateAsync(req, ct);
            await ReanudarTrasRechazoAsync(req, ct);
            return req;
        }

        // Si hay más aprobadores pendientes, mantener en Delegado (cadena secuencial).
        if (pendientes > 0)
        {
            req.Estado = EstadoAprobacion.Delegado;
            await _reqRepo.UpdateAsync(req, ct);
            return req;
        }

        // No hay más pendientes: evaluar política para resolución final.
        bool cumple = false;
        if (policy?.RequiereUnanimidad == true)
            cumple = req.Asignados.All(a => a.Estado == "Aprobado" || a.Estado == "Delegado");
        else if (policy != null)
            cumple = aprobados >= policy.CantidadMinimaAprobaciones;
        else
            cumple = aprobados >= 1; // aprobación simple por defecto

        if (cumple)
        {
            req.Estado = EstadoAprobacion.Aprobado;
            await _reqRepo.UpdateAsync(req, ct);
            await ReanudarTrasAprobacionAsync(req, ct);
        }
        else
        {
            req.Estado = EstadoAprobacion.EnRevision;
            await _reqRepo.UpdateAsync(req, ct);
        }

        return req;
    }

    /// <summary>
    /// Delegación de una aprobación a otro usuario (actividad 7). Solo si la política lo permite (Regla 5).
    /// Queda registrada en auditoría.
    /// </summary>
    public async Task<ApprovalRequest> DelegarAsync(
        int idApproval, int idUsuario, int idUsuarioDestino, string? comentario, CancellationToken ct = default)
    {
        var req = await _reqRepo.GetByIdAsync(idApproval, ct)
                  ?? throw new InvalidOperationException($"Solicitud {idApproval} no encontrada.");

        if (idUsuarioDestino <= 0)
            throw new InvalidOperationException("Debe indicar un ID de usuario destino para delegar la solicitud.");

        // El destino debe existir, estar activo y tener rol aprobador (si no, la
        // solicitud quedaría irresoluble: el rol Usuario no accede al Centro de Aprobaciones).
        var destino = await _usuarioRepo.GetByIdAsync(idUsuarioDestino);
        if (destino == null || !destino.Activo)
            throw new InvalidOperationException($"El usuario destino {idUsuarioDestino} no existe o está inactivo.");
        if (!TieneRolAprobador(destino))
            throw new InvalidOperationException(
                $"El usuario destino {idUsuarioDestino} no tiene un rol habilitado para aprobar (se requiere Administrador, Operador o Supervisor).");

        if (req.Policy != null && !req.Policy.PermiteDelegacion)
            throw new InvalidOperationException("La política no permite delegación.");

        var asignado = req.Asignados.FirstOrDefault(a => a.IdUsuario == idUsuario && a.Estado == "Pendiente")
                       ?? throw new UnauthorizedAccessException("No autorizado para delegar esta solicitud.");

        // Registrar la delegación como decisión de auditoría.
        await _decRepo.AddAsync(new ApprovalDecision
        {
            IdApproval = idApproval,
            IdUsuario = idUsuario,
            Decision = "Delegar",
            Comentario = $"Delegado a usuario {idUsuarioDestino}. {comentario}",
            FechaDecision = DateTime.UtcNow
        }, ct);

        // El asignado original queda resuelto por delegación y se agrega el nuevo.
        asignado.Estado = "Delegado";
        await _asgRepo.UpdateAsync(asignado, ct);

        await _asgRepo.AddAsync(new ApprovalAssignee
        {
            IdApproval = idApproval,
            IdUsuario = idUsuarioDestino,
            EsPrincipal = asignado.EsPrincipal,
            Estado = "Pendiente"
        }, ct);

        req.Estado = EstadoAprobacion.Delegado;
        await _reqRepo.UpdateAsync(req, ct);

        await RegistrarAuditoriaAsync(req.IdPlan, idApproval, "DelegacionAprobacion",
            $"Usuario {idUsuario} delegó a {idUsuarioDestino}. {comentario}", ct);

        return req;
    }

    /// <summary>
    /// Marca la solicitud como expirada (actividad 8). El comportamiento posterior es configurable
    /// vía política; por defecto se cancela el plan (Regla 4: respeta la política configurada).
    /// </summary>
    public async Task<ApprovalRequest> ExpirarAsync(int idApproval, CancellationToken ct = default)
    {
        var req = await _reqRepo.GetByIdAsync(idApproval, ct)
                  ?? throw new InvalidOperationException($"Solicitud {idApproval} no encontrada.");
        if (req.Estado is EstadoAprobacion.Aprobado or EstadoAprobacion.Rechazado
            or EstadoAprobacion.Expirado or EstadoAprobacion.Cancelado)
            return req;

        req.Estado = EstadoAprobacion.Expirado;
        await _reqRepo.UpdateAsync(req, ct);
        await RegistrarAuditoriaAsync(req.IdPlan, idApproval, "AprobacionExpirada",
            "La solicitud venció sin decisión.", ct);

        // Regla 4: la política configurada determina el comportamiento. Por defecto: cancelar plan.
        var plan = await _planRepo.GetByIdAsync(req.IdPlan, ct);
        if (plan != null)
        {
            plan.Estado = "Cancelado";
            await _planRepo.UpdateEstadoAsync(req.IdPlan, "Cancelado", DateTime.UtcNow, ct);
            await RegistrarAuditoriaAsync(req.IdPlan, idApproval, "PlanCancelado",
                "Plan cancelado por aprobación expirada.", ct);
        }
        return req;
    }

    // --- Reanudación automática (actividad 9 y 10) ---

    private async Task ReanudarTrasAprobacionAsync(ApprovalRequest req, CancellationToken ct)
    {
        var plan = await _planRepo.GetByIdAsync(req.IdPlan, ct);
        if (plan == null) return;
        plan.Aprobado = true;
        await _planRepo.UpdateAsync(plan, ct);
        await RegistrarAuditoriaAsync(req.IdPlan, req.IdApproval, "PlanReanudado",
            "Aprobación aceptada. Reanudando ejecución del plan.", ct);

        // Reanudación POR EVENTO con scope propio: relanza el grafo validado.
        // No se espera en memoria (un reinicio huérfana la espera).
        var idPlan = req.IdPlan;
        _ = Task.Run(async () =>
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var planner = scope.ServiceProvider.GetRequiredService<IPlannerEngine>();
                await planner.ContinuarPlanAprobadoAsync(idPlan, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al reanudar plan {IdPlan} tras aprobación.", idPlan);
            }
        });
    }

    private async Task ReanudarTrasRechazoAsync(ApprovalRequest req, CancellationToken ct)
    {
        var plan = await _planRepo.GetByIdAsync(req.IdPlan, ct);
        if (plan == null) return;
        plan.Estado = "Cancelado";
        await _planRepo.UpdateEstadoAsync(req.IdPlan, "Cancelado", DateTime.UtcNow, ct);
        await RegistrarAuditoriaAsync(req.IdPlan, req.IdApproval, "PlanCancelado",
            "Aprobación rechazada. Plan cancelado.", ct);
    }

    private async Task RegistrarAuditoriaAsync(int idPlan, int idApproval, string evento, string detalle, CancellationToken ct)
    {
        try
        {
            await _logRepo.AddAsync(new PlanExecutionLog
            {
                IdPlan = idPlan,
                Evento = evento,
                Detalle = $"[{idApproval}] {detalle}",
                Fecha = DateTime.UtcNow
            }, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo registrar auditoría de aprobación {Evento}", evento);
        }
    }
}
