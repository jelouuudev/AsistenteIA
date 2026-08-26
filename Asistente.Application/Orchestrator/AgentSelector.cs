using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Asistente.Application.Interfaces;
using Asistente.Application.Orchestrator;
using Asistente.Domain.Entities;
using Asistente.Domain.Interfaces;

namespace Asistente.Application.Orchestrator;

/// <summary>
/// Implementación de IAgentSelector (RF Actividad 2).
/// Selecciona agentes colaboradores según:
///  - Tipo de solicitud (detecta intención SQL/RAG/Reporte en el texto).
///  - Capacidades (herramientas asignadas al agente).
///  - Permisos (reglas de colaboración desde el agente principal).
///  - Prioridad y límites de profundidad/max agentes.
/// </summary>
public class AgentSelector : IAgentSelector
{
    private readonly IAsistenteRepository _asistenteRepository;
    private readonly IAgentCollaborationRuleRepository _reglasRepo;
    private readonly IAutorizacionService _autorizacion;

    public AgentSelector(
        IAsistenteRepository asistenteRepository,
        IAgentCollaborationRuleRepository reglasRepo,
        IAutorizacionService autorizacion)
    {
        _asistenteRepository = asistenteRepository;
        _reglasRepo = reglasRepo;
        _autorizacion = autorizacion;
    }

    public async Task<IEnumerable<AgentCandidate>> SelectAgentsAsync(
        AgentRequest request, CancellationToken cancellationToken = default)
    {
        var principal = await _asistenteRepository.GetByIdAsync(request.IdAgentePrincipal);
        if (principal == null) return new List<AgentCandidate>();

        var todos = (await _asistenteRepository.GetAllAsync())
            .Where(a => a.Activo && a.IdAsistente != request.IdAgentePrincipal)
            .ToList();

        // Carga determinista de herramientas por agente (AsNoTracking), independiente del
        // Include de GetAllAsync y del identity-map del DbContext compartido.
        var toolCodes = new Dictionary<int, HashSet<string>>();
        foreach (var a in todos)
        {
            var codigos = await _asistenteRepository.GetHerramientasActivasAsync(a.IdAsistente);
            toolCodes[a.IdAsistente] = codigos.ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        var candidatos = new List<AgentCandidate>();
        var pregunta = request.Pregunta.ToLowerInvariant();

        // Si el usuario sugirió agentes explícitamente, respetarlos (sujeos a reglas/permisos).
        var sugeridos = request.AgentesSugeridos?.ToHashSet() ?? new HashSet<int>();

        foreach (var agente in todos)
        {
            var score = 0.0;
            var rol = "Colaborador";
            var depende = new List<int>();

            var tools = toolCodes.GetValueOrDefault(agente.IdAsistente, new HashSet<string>());
            var tieneSql = tools.Contains("SqlQueryTool");
            var tieneRag = tools.Contains("DocumentSearchTool");
            var tieneReporte = tools.Contains("ReportTool");

            // --- Detección por tipo de solicitud (intención) ---
            if (tieneSql && (pregunta.Contains("venta") || pregunta.Contains("activo") ||
                             pregunta.Contains("sql") || pregunta.Contains("consulta") ||
                             pregunta.Contains("datos") || pregunta.Contains("cliente") ||
                             pregunta.Contains("emitid") || pregunta.Contains("registr")))
            {
                score += 3;
                rol = "SQL";
                if (pregunta.Contains("compar") || pregunta.Contains("resumen") || pregunta.Contains("ejecutiv"))
                    depende.Add(request.IdAgentePrincipal); // el reporte/comparación depende del dato
            }

            if (tieneRag && (pregunta.Contains("procedimient") || pregunta.Contains("manual") ||
                             pregunta.Contains("política") || pregunta.Contains("normativa") ||
                             pregunta.Contains("document") || pregunta.Contains("vacacion") ||
                             pregunta.Contains("registrar una factura")))
            {
                score += 3;
                rol = "RAG";
            }

            if (tieneReporte && (pregunta.Contains("resumen") || pregunta.Contains("reporte") ||
                                 pregunta.Contains("compar") || pregunta.Contains("ejecutiv") ||
                                 pregunta.Contains("semanal") || pregunta.Contains("prepar")))
            {
                score += 2;
                rol = "Reporte";
                // El reporte consolida datos previos (SQL y RAG)
                depende.Add(request.IdAgentePrincipal);
            }

            if (sugeridos.Contains(agente.IdAsistente))
                score += 5;

            if (score <= 0) continue;

            // --- Permisos: regla de colaboración desde el agente principal ---
            var permitido = await _reglasRepo.EstaPermitidoAsync(request.IdAgentePrincipal, agente.IdAsistente, cancellationToken);
            if (!permitido) continue; // Regla 3: solo autorizados por regla de colaboración

            // Nota: la colaboración multi-agente se rige por AgentCollaborationRule (Regla 3),
            // no por la asignación directa de asistentes al usuario (que aplica al chat 1-a-1).
            // Por eso NO se filtra aquí por _autorizacion.VerificarAsistenteAsync.

            candidatos.Add(new AgentCandidate
            {
                IdAgente = agente.IdAsistente,
                Nombre = agente.Nombre,
                Objetivo = agente.Objetivo ?? "",
                Rol = rol,
                Prioridad = agente.Version,
                DependeDe = depende.Distinct().ToList(),
                Puntuacion = score
            });
        }

        // Ordenar por puntuación descendente y limitar a los más relevantes.
        return candidatos
            .OrderByDescending(c => c.Puntuacion)
            .ThenBy(c => c.IdAgente)
            .ToList();
    }

    private static bool AgenteTieneHerramienta(Asistente.Domain.Entities.Asistente agente, string codigo)
        => agente.AsistentesHerramientas.Any(h => h.Activa &&
            (h.Herramienta?.Codigo?.Equals(codigo, StringComparison.OrdinalIgnoreCase) ?? false));
}
