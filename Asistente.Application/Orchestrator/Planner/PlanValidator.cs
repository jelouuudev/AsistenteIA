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
/// Verifica permisos, agentes, herramientas, workflows, políticas, restricciones, riesgos
/// y que el grafo no tenga ciclos (DAG). Todo plan se valida antes de ejecutarse (Regla 1).
/// B-04: Valida la cadena completa: usuario → rol → agente → herramienta/workflow → política.
/// </summary>
public class PlanValidator
{
    private readonly IAsistenteRepository _asistenteRepo;
    private readonly IAutorizacionService _autorizacionService;
    private readonly IUsuarioRepository _usuarioRepo;
    private readonly IPoliticaIARepository _politicaRepo;
    private readonly IWorkflowRepository _workflowRepo;

    public PlanValidator(
        IAsistenteRepository asistenteRepo,
        IAutorizacionService autorizacionService,
        IUsuarioRepository usuarioRepo,
        IPoliticaIARepository politicaRepo,
        IWorkflowRepository workflowRepo)
    {
        _asistenteRepo = asistenteRepo;
        _autorizacionService = autorizacionService;
        _usuarioRepo = usuarioRepo;
        _politicaRepo = politicaRepo;
        _workflowRepo = workflowRepo;
    }

    public async Task<ResultadoValidacionPlan> ValidarAsync(Plan plan, CancellationToken ct = default)
    {
        var resultado = new ResultadoValidacionPlan();
        var agentes = (await _asistenteRepo.GetAllAsync()).ToList();
        var porId = agentes.ToDictionary(a => a.IdAsistente);

        // 0) B-04: Validar usuario → rol → agente → herramienta/workflow → política
        var usuario = await _usuarioRepo.GetByIdAsync(plan.IdUsuario);
        if (usuario == null)
        {
            resultado.Errores.Add($"El usuario {plan.IdUsuario} no existe.");
            resultado.Valido = false;
            return resultado;
        }

        if (!usuario.Activo)
        {
            resultado.Errores.Add($"El usuario '{usuario.UsuarioNombre}' está inactivo.");
            resultado.Valido = false;
            return resultado;
        }

        // Verificar roles del usuario
        var rolesUsuario = usuario.UsuarioRoles.Select(ur => ur.Rol?.Nombre).Where(n => !string.IsNullOrEmpty(n)).ToList();
        bool esAdmin = rolesUsuario.Contains("Administrador");
        bool esSupervisor = rolesUsuario.Contains("Supervisor");

        // B-04: Verificar política de máximo de pasos por plan
        var politicaMaxPasos = await _politicaRepo.GetByTipoAsync("MaxPasosPlan", ct);
        if (politicaMaxPasos != null && politicaMaxPasos.Activa && int.TryParse(politicaMaxPasos.Valor, out int maxPasos))
        {
            if (plan.Pasos.Count > maxPasos)
            {
                resultado.Errores.Add($"El plan excede el máximo de pasos permitidos ({maxPasos}).");
            }
        }

        // 1) Agentes referenciados existen, están activos Y el usuario está autorizado.
        foreach (var paso in plan.Pasos.Where(p => p.IdAsistente.HasValue))
        {
            int idAsistente = paso.IdAsistente.Value;

            // Verificar existencia y estado del agente
            if (!porId.TryGetValue(idAsistente, out var a) || !a.Activo)
            {
                resultado.Errores.Add($"El paso '{paso.Nombre}' referencia un agente inexistente o inactivo (Id {idAsistente}).");
                continue;
            }

            // B-04: Verificar que el usuario está autorizado para usar este agente (rol → agente)
            if (!esAdmin)
            {
                var authAgente = await _autorizacionService.VerificarAsistenteAsync(plan.IdUsuario, idAsistente, ct);
                if (!authAgente.Permitido)
                {
                    resultado.Errores.Add($"El paso '{paso.Nombre}': {authAgente.Motivo}");
                }
            }
        }

        // 2) Herramientas referenciadas existen Y el usuario tiene permiso.
        var herramientasValidas = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "SqlQueryTool", "DocumentSearchTool", "ReportTool", "CalculatorTool" };

        foreach (var paso in plan.Pasos.Where(p => !string.IsNullOrWhiteSpace(p.CodigoHerramienta)))
        {
            if (!herramientasValidas.Contains(paso.CodigoHerramienta!))
            {
                resultado.Advertencias.Add($"El paso '{paso.Nombre}' usa la herramienta '{paso.CodigoHerramienta}' no registrada.");
            }
            else if (paso.IdAsistente.HasValue)
            {
                // B-04: Verificar autorización real del usuario para usar esta herramienta
                if (!esAdmin)
                {
                    var auth = await _autorizacionService.VerificarHerramientaAsync(
                        plan.IdUsuario, paso.IdAsistente.Value, paso.CodigoHerramienta!, ct);
                    if (!auth.Permitido)
                    {
                        resultado.Errores.Add($"El paso '{paso.Nombre}': {auth.Motivo}");
                    }
                }
            }
        }

        // 3) B-04: Workflows referenciados existen, están activos Y cumplen política.
        foreach (var paso in plan.Pasos.Where(p => p.Tipo == "Workflow" && p.IdWorkflow.HasValue))
        {
            int idWorkflow = paso.IdWorkflow.Value;
            var workflow = await _workflowRepo.GetByIdAsync(idWorkflow, ct);

            if (workflow == null)
            {
                resultado.Errores.Add($"El paso '{paso.Nombre}' referencia un workflow inexistente (Id {idWorkflow}).");
                continue;
            }

            if (workflow.Estado != EstadoWorkflow.Activo)
            {
                resultado.Errores.Add($"El paso '{paso.Nombre}' referencia el workflow '{workflow.Nombre}' que no está activo.");
                continue;
            }

            // B-04: Verificar que el usuario puede ejecutar este workflow por política
            if (!esAdmin)
            {
                var politicaWorkflow = await _politicaRepo.GetByTipoAsync("WorkflowsPermitidos", ct);
                if (politicaWorkflow != null && politicaWorkflow.Activa)
                {
                    var workflowsPermitidos = politicaWorkflow.Valor.Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(v => v.Trim()).ToList();
                    if (!workflowsPermitidos.Contains(workflow.Codigo) && !workflowsPermitidos.Contains("*"))
                    {
                        resultado.Errores.Add($"El paso '{paso.Nombre}': El workflow '{workflow.Codigo}' no está permitido por política.");
                    }
                }
            }
        }

        // 4) Sin pasos vacíos.
        if (plan.Pasos.Count == 0)
            resultado.Errores.Add("El plan no contiene pasos.");

        // 5) Riesgos: acciones sensibles (solo admin/supervisor puede ejecutar planes con Approval).
        if (plan.Pasos.Any(p => p.Tipo == "Approval"))
        {
            resultado.Riesgos.Add("El plan contiene acciones que requieren aprobación humana.");
            if (!esAdmin && !esSupervisor)
            {
                resultado.Errores.Add("Solo Administradores y Supervisores pueden crear planes que requieren aprobación.");
            }
        }

        // 6) Sin ciclos (Regla 7): recorrido topológico de las dependencias.
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