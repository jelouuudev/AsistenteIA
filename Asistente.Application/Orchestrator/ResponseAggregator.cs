using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Asistente.Application.Interfaces;
using Asistente.Domain.Entities;

namespace Asistente.Application.Orchestrator;

/// <summary>
/// Implementación de IResponseAggregator (RF Actividad 7).
/// Ordena los resultados, elimina duplicados, preserva referencias/contexto y
/// genera una única respuesta coherente (Regla 7: una sola respuesta consolidada).
/// </summary>
public class ResponseAggregator : IResponseAggregator
{
    public Task<string> BuildFinalResponseAsync(AgentExecution execution, CancellationToken cancellationToken = default)
    {
        var partes = new List<string>();
        var vistos = new HashSet<string>();

        // Ordenar por Orden de los pasos para mantener coherencia narrativa.
        var pasos = execution.Pasos
            .Where(p => p.Estado == "Completado" && !string.IsNullOrWhiteSpace(p.Resultado))
            .OrderBy(p => p.Orden)
            .ToList();

        foreach (var paso in pasos)
        {
            var clave = paso.Resultado!.Trim();
            // Eliminar duplicados (misma respuesta de distintos agentes).
            if (vistos.Contains(clave)) continue;
            vistos.Add(clave);

            var encabezado = $"### {paso.Agente?.Nombre ?? "Agente"} — {paso.Accion}";
            partes.Add($"{encabezado}\n{paso.Resultado.Trim()}");
        }

        if (partes.Count == 0)
            return Task.FromResult("No se obtuvo información de los agentes colaboradores para esta solicitud.");

        var consolidado = string.Join("\n\n", partes);

        // Encabezado de trazabilidad para el usuario (Regla 6 / Actividad 13).
        var traza = $"\n\n---\n_Respuesta consolidada por el Agent Orchestrator · " +
                    $"{execution.CantidadAgentes} agente(s) · profundidad {execution.ProfundidadAlcanzada} · " +
                    $"{execution.HerramientasUtilizadas} herramienta(s) ejecutada(s)._";

        return Task.FromResult(consolidado + traza);
    }
}
