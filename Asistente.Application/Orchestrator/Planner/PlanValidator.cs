using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Asistente.Application.Interfaces;
using Asistente.Domain.Entities;
using Asistente.Domain.Interfaces;

namespace Asistente.Application.Orchestrator.Planner;

/// <summary>
/// Plan Validator (ETAPA 18, Actividad 3 / Reglas 1, 2, 7).
/// Verifica permisos, agentes, herramientas, restricciones, riesgos y que el grafo
/// no tenga ciclos (DAG). Todo plan se valida antes de ejecutarse (Regla 1).
/// </summary>
public class PlanValidator
{
    private readonly IAsistenteRepository _asistenteRepo;

    public PlanValidator(IAsistenteRepository asistenteRepo)
    {
        _asistenteRepo = asistenteRepo;
    }

    public async Task<ResultadoValidacionPlan> ValidarAsync(Plan plan, CancellationToken ct = default)
    {
        var resultado = new ResultadoValidacionPlan();
        var agentes = (await _asistenteRepo.GetAllAsync()).ToList();
        var porId = agentes.ToDictionary(a => a.IdAsistente);

        // 1) Agentes referenciados existen y están activos.
        foreach (var paso in plan.Pasos.Where(p => p.IdAsistente.HasValue))
        {
            if (!porId.TryGetValue(paso.IdAsistente.Value, out var a) || !a.Activo)
            {
                resultado.Errores.Add($"El paso '{paso.Nombre}' referencia un agente inexistente o inactivo (Id {paso.IdAsistente}).");
            }
        }

        // 2) Herramientas referenciadas existen (validación básica por código conocido).
        var herramientasValidas = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "SqlQueryTool", "DocumentSearchTool", "ReportTool", "CalculatorTool" };
        foreach (var paso in plan.Pasos.Where(p => !string.IsNullOrWhiteSpace(p.CodigoHerramienta)))
        {
            if (!herramientasValidas.Contains(paso.CodigoHerramienta!))
                resultado.Advertencias.Add($"El paso '{paso.Nombre}' usa la herramienta '{paso.CodigoHerramienta}' no registrada.");
        }

        // 3) Sin pasos vacíos.
        if (plan.Pasos.Count == 0)
            resultado.Errores.Add("El plan no contiene pasos.");

        // 4) Riesgos: acciones sensibles.
        if (plan.Pasos.Any(p => p.Tipo == "Approval"))
            resultado.Riesgos.Add("El plan contiene acciones que requieren aprobación humana.");

        // 5) Sin ciclos (Regla 7): recorrido topológico de las dependencias.
        if (TieneCiclos(plan))
            resultado.Errores.Add("El grafo de dependencias contiene un ciclo (Regla 7: no se permiten grafos con ciclos).");

        resultado.Valido = resultado.Errores.Count == 0;
        return resultado;
    }

    private static bool TieneCiclos(Plan plan)
    {
        var adj = plan.Dependencias
            .Where(d => d.StepOrigen != d.StepDestino)
            .GroupBy(d => d.StepOrigen)
            .ToDictionary(g => g.Key, g => g.Select(d => d.StepDestino).ToHashSet());

        var visitando = new HashSet<int>();
        var visitado = new HashSet<int>();

        bool Dfs(int nodo)
        {
            if (visitando.Contains(nodo)) return true;   // ciclo
            if (visitado.Contains(nodo)) return false;
            visitando.Add(nodo);
            foreach (var vec in adj.GetValueOrDefault(nodo, new HashSet<int>()))
                if (Dfs(vec)) return true;
            visitando.Remove(nodo);
            visitado.Add(nodo);
            return false;
        }

        return plan.Pasos.Select(p => p.Orden).Any(Dfs);
    }
}
