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
    private readonly ILogger<MemoryOrchestrator> _logger;

    public MemoryOrchestrator(
        MemoryManager manager,
        IMemoryRepository repo,
        ILogger<MemoryOrchestrator> logger)
    {
        _manager = manager;
        _repo = repo;
        _logger = logger;
    }

    /// <summary>
    /// Arma el bloque "[Memoria {ámbito}: {título}] ..." con lo autorizado.
    /// Vacío si no hay nada aplicable: nunca falla la operación principal.
    /// </summary>
    public async Task<string> ObtenerContextoAsync(int idUsuario, int? idAsistente = null,
        int? idWorkflow = null, int maxCaracteres = 3000, CancellationToken ct = default)
    {
        try
        {
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

            var sb = new StringBuilder();
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
