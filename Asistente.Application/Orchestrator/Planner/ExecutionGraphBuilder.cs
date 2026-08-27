using System;
using System.Collections.Generic;
using System.Linq;
using Asistente.Application.Orchestrator;
using Asistente.Domain.Entities;

namespace Asistente.Application.Orchestrator.Planner;

/// <summary>
/// Execution Graph Builder (ETAPA 18, Actividad 4). Transforma un plan validado en un
/// grafo ejecutable (DAG) reutilizando la estructura del Agent Orchestrator (ETAPA 17).
/// Detecta dependencias y paralelismo a partir de PlanDependency.
/// </summary>
public class ExecutionGraphBuilder
{
    /// <summary>Convierte los pasos + dependencias del plan en un ExecutionGraph.</summary>
    public ExecutionGraph Construir(Plan plan)
    {
        var grafo = new ExecutionGraph();
        var porOrden = plan.Pasos.ToDictionary(p => p.Orden);

        foreach (var paso in plan.Pasos.OrderBy(p => p.Orden))
        {
            var dependeDe = plan.Dependencias
                .Where(d => d.StepDestino == paso.Orden)
                .Select(d => d.StepOrigen)
                .ToList();

            grafo.Nodos.Add(new ExecutionNode
            {
                IdNodo = paso.Orden,
                IdAgente = paso.IdAsistente ?? 0,
                NombreAgente = ObtenerNombreAgente(plan, paso),
                Accion = $"{paso.Tipo}: {paso.Nombre}",
                PreguntaAsignada = paso.Descripcion ?? paso.Nombre,
                DependeDe = dependeDe,
                Estado = "Pendiente",
                EsAprobacion = paso.Tipo == "Approval"
            });
        }

        return grafo;
    }

    private static string ObtenerNombreAgente(Plan plan, PlanStep paso)
    {
        // El nombre del agente se resuelve en tiempo de ejecución por el Orchestrator;
        // aquí registramos el disponible en el paso.
        return paso.IdAsistente.HasValue
            ? plan.Pasos.FirstOrDefault(p => p.IdAsistente == paso.IdAsistente)?.Nombre ?? "Agente"
            : "Agente";
    }
}
