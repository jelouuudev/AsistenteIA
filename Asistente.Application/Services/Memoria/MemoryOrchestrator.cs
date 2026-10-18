using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Asistente.Application.Interfaces;
using Asistente.Shared;
using Microsoft.Extensions.Logging;
namespace Asistente.Application.Services.Memoria;

/// <summary>
/// Orquestador de memoria (ETAPA 21): compone el contexto operativo para un
/// agente/usuario/workflow con las memorias autorizadas de cada ámbito
/// (Workflow > Agente > Usuario > Organización), recortado a un tope.
/// Es lo que consumen el Agent Runtime (ChatService, siempre en cada mensaje) y
/// el Workflow Engine (estado persistente + token {{memoria}} en sus pasos).
/// El Planner no lo invoca directamente a propósito: sus pasos Agent ejecutan el
/// Agent Runtime, que ya inyecta la memoria; llamarlo otra vez duplicaría el
/// bloque sin aportar nada.
/// </summary>
public class MemoryOrchestrator
{
    /// <summary>
    /// Tope de memorias que se evaluan al componer contexto. Cada candidata se
    /// autoriza con una consulta de permisos, así que sin este tope el coste crece
    /// con el número de memorias activas y el runtime paga la factura en cada mensaje.
    /// </summary>
    internal const int TopeMemoriasContexto = 20;

    private readonly MemoryManager _manager;
    private readonly IMemoryRepository _repo;
    private readonly IMemoriaService _conversaciones;
    private readonly ILogger<MemoryOrchestrator> _logger;

    public MemoryOrchestrator(
        MemoryManager manager,
        IMemoryRepository repo,
        IMemoriaService conversaciones,
        ILogger<MemoryOrchestrator> logger)
    {
        _manager = manager;
        _repo = repo;
        _conversaciones = conversaciones;
        _logger = logger;
    }

    /// <summary>
    /// Arma el bloque "[Memoria {ámbito}: {título}] ..." con las autorizadas.
    /// Vacío si no hay nada: nunca falla la operación principal.
    /// Si se indica idConversacion, antepone los últimos N turnos del historial
    /// (memoria conversacional persistente reutilizando el almacenamiento de
    /// chat existente, sin duplicar datos). Solo de su propietario.
    /// </summary>
    public virtual async Task<string> ObtenerContextoAsync(int idUsuario, int? idAsistente = null,
        int? idWorkflow = null, int maxCaracteres = 3000, int? idConversacion = null,
        int maxTurnos = 6, CancellationToken ct = default)
    {
        try
        {
            var sb = new StringBuilder();

            if (idConversacion.HasValue)
            {
                var bloque = await BloqueConversacionalAsync(idUsuario, idConversacion.Value, maxTurnos, ct);
                if (!string.IsNullOrWhiteSpace(bloque))
                {
                    sb.Append(Recortar(bloque, maxCaracteres));
                    sb.Append('\n');
                }
            }

            var piezas = new List<(int Orden, MemoryDto M)>();
            var todas = await _manager.ListarAsync(idUsuario, null, ct, TopeMemoriasContexto);

            foreach (var m in todas)
            {
                int orden = m.Ambito switch
                {
                    "Workflow" when idWorkflow.HasValue && m.IdPropietario == idWorkflow => 0,
                    "Agente" when idAsistente.HasValue && m.IdPropietario == idAsistente => 1,
                    "Usuario" when m.IdPropietario == idUsuario => 2,
                    "Organizacion" => 3,
                    // El ámbito Conversacional sí es inyectable: son notas y
                    // antecedentes que alguien registró como memoria (no el historial
                    // del chat, que va aparte). Antes caía en la rama de descarte y
                    // una memoria de este ámbito nunca llegaba al prompt.
                    "Conversacional" => 4,
                    _ => 9
                };
                if (orden < 9) piezas.Add((orden, m));
            }

            foreach (var (_, m) in piezas.OrderBy(p => p.Orden).ThenBy(p => p.M.Codigo))
            {
                var bloque = $"[Memoria {m.Ambito}: {m.Titulo}]\n{m.Contenido}\n";
                if (sb.Length + bloque.Length > maxCaracteres) break;
                sb.Append(bloque);
            }
            return sb.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Memoria: no se pudo componer contexto.");
            return string.Empty;
        }
    }

    /// <summary>
    /// Últimos turnos de una conversación del propio usuario, como bloque de
    /// memoria conversacional. Solo del propietario; ajenas devuelven vacío
    /// (sin filtrar nada: ni siquiera se confirma su existencia).
    /// </summary>
    internal async Task<string> BloqueConversacionalAsync(int idUsuario, int idConversacion,
        int maxTurnos, CancellationToken ct)
    {
        try
        {
            var conversacion = await _conversaciones.ObtenerConversacionAsync(idConversacion);
            if (conversacion == null
                || conversacion.UsuarioPropietario != idUsuario
                || !string.Equals(conversacion.Estado, "Activa", StringComparison.OrdinalIgnoreCase))
                return string.Empty;
            var turnos = (conversacion.Mensajes ?? new List<MensajeDto>())
                .Where(m => !string.IsNullOrWhiteSpace(m.Contenido))
                .TakeLast(Math.Max(maxTurnos, 1))
                .Select(m => $"{(m.Rol ?? "usuario").Trim().ToLowerInvariant()}: {m.Contenido.Trim()}");
            var cuerpo = string.Join("\n", turnos);
            if (string.IsNullOrWhiteSpace(cuerpo)) return string.Empty;
            return $"[Memoria Conversacional: {conversacion.Titulo ?? $"conversación {idConversacion}"}]\n{cuerpo}\n";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Memoria: no se pudo leer la conversación {Id}.", idConversacion);
            return string.Empty;
        }
    }

    /// <summary>Caso 3: el workflow guarda su estado para continuar después
    /// (crea la primera vez, versiona las siguientes).</summary>
    public virtual async Task<MemoryDto> GuardarEstadoWorkflowAsync(int idWorkflow, string codigo,
        string titulo, string contenido, int idUsuario, CancellationToken ct = default)
    {
        // La existencia se consulta en el repositorio y no con ObtenerAsync: ese
        // método además evalúa expiración y permisos, y un estado de workflow que
        // venció (política de 365 días, sin permiso asociado) ya no podría volver
        // a guardarse. El estado de un flujo es del propio flujo, no una memoria
        // sujeta a autorización de lectura.
        var existente = await _repo.GetByCodigoAsync(codigo.Trim(), ct);
        if (existente != null && existente.Activo)
        {
            return await _manager.ActualizarAsync(codigo, new ActualizarMemoryRequest
            {
                Contenido = contenido,
                Motivo = $"Estado del workflow {idWorkflow}."
            }, idUsuario, ct, ignorarExpiracion: true);
        }

        return await _manager.CrearAsync(new CrearMemoryRequest
        {
            Codigo = codigo,
            Titulo = titulo,
            Contenido = contenido,
            Ambito = "Workflow",
            IdPropietario = idWorkflow,
            RequierePermiso = false
        }, idUsuario, ct);
    }

    /// <summary>Caso 3: el workflow retoma su estado guardado.</summary>
    public virtual Task<MemoryDto> LeerEstadoWorkflowAsync(int idWorkflow, string codigo,
        int idUsuario, CancellationToken ct = default)
        => _manager.ObtenerAsync(codigo, idUsuario, null, ct);

    /// <summary>
    /// Igual que <see cref="LeerEstadoWorkflowAsync"/> pero devuelve null en lugar de
    /// lanzar cuando no hay estado guardado. El Workflow Engine lo invoca en cada
    /// ejecución y no debe convertir "no hay estado previo" en un error del flujo.
    /// </summary>
    public virtual async Task<MemoryDto?> TryLeerEstadoWorkflowAsync(string codigo,
        int idUsuario, CancellationToken ct = default)
    {
        try
        {
            return await _manager.ObtenerAsync(codigo, idUsuario, null, ct);
        }
        catch (KeyNotFoundException)
        {
            return null;
        }
    }

    /// <summary>Corta un bloque a <paramref name="maxCaracteres"/> sin partir una línea,
    /// para respetar el presupuesto de tokens del prompt.</summary>
    private static string Recortar(string texto, int maxCaracteres)
    {
        if (maxCaracteres <= 0 || texto.Length <= maxCaracteres) return texto;
        var recorte = texto[..maxCaracteres];
        var ultimoSalto = recorte.LastIndexOf('\n');
        if (ultimoSalto > maxCaracteres / 2) recorte = recorte[..ultimoSalto];
        return recorte + "… (memoria recortada)";
    }
}
