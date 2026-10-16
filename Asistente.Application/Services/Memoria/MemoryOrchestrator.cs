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
/// Es lo que consumen Agent Runtime, Planner y Workflow Engine.
/// </summary>
public class MemoryOrchestrator
{
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
    public async Task<string> ObtenerContextoAsync(int idUsuario, int? idAsistente = null,
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
                    sb.Append(bloque);
                    sb.Append('\n');
                }
            }

            var piezas = new List<(int Orden, MemoryDto M)>();
            var todas = await _manager.ListarAsync(idUsuario, null, ct);

            foreach (var m in todas)
            {
                int orden = m.Ambito switch
                {
                    "Workflow" when idWorkflow.HasValue && m.IdPropietario == idWorkflow => 0,
                    "Agente" when idAsistente.HasValue && m.IdPropietario == idAsistente => 1,
                    "Usuario" when m.IdPropietario == idUsuario => 2,
                    "Organizacion" => 3,
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
    public async Task<MemoryDto> GuardarEstadoWorkflowAsync(int idWorkflow, string codigo,
        string titulo, string contenido, int idUsuario, CancellationToken ct = default)
    {
        try
        {
            await _manager.ObtenerAsync(codigo, idUsuario, null, ct);
            return await _manager.ActualizarAsync(codigo, new ActualizarMemoryRequest
            {
                Contenido = contenido,
                Motivo = $"Estado del workflow {idWorkflow}."
            }, idUsuario, ct);
        }
        catch (KeyNotFoundException)
        {
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
    }

    /// <summary>Caso 3: el workflow retoma su estado guardado.</summary>
    public Task<MemoryDto> LeerEstadoWorkflowAsync(int idWorkflow, string codigo,
        int idUsuario, CancellationToken ct = default)
        => _manager.ObtenerAsync(codigo, idUsuario, null, ct);
}
