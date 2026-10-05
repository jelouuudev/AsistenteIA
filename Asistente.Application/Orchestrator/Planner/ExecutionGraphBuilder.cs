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

        // Un nodo por ORDEN. Se deduplica porque plan.Pasos es una List<PlanStep> que,
        // tras un read + un UPDATE con tracking en el mismo DbContext, puede llegar con
        // el mismo paso repetido (fixup de navegaciones de EF). Antes este código hacía
        // `plan.Pasos.ToDictionary(p => p.Orden)` —además muerto, no se usaba— y
        // reventaba con "An item with the same key has already been added. Key: 0",
        // dejando el plan colgado en IniciandoEjecucion para siempre (el background
        // moría antes de LanzarEjecucionGrafo -> UpdateEstadoAsync("EnEjecucion")).
        foreach (var paso in plan.Pasos
                     .GroupBy(p => p.Orden)
                     .OrderBy(g => g.Key)
                     .Select(g => g.First()))
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
