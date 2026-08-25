using System;
using System.Threading;
using System.Threading.Tasks;
using Asistente.Application.Interfaces;
using Asistente.Application.Orchestrator;
using Asistente.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Asistente.Application.Orchestrator.Planner;

/// <summary>
/// Planner Engine (ETAPA 18). Orquesta Plan Builder, Plan Validator, Execution Graph Builder
/// y Execution Supervisor. Convierte lenguaje natural en un plan ejecutable y lo delega al
/// Agent Orchestrator (ETAPA 17) — NUNCA ejecuta acciones directamente (Regla 3).
/// </summary>
public class PlannerEngine : IPlannerEngine
{
    private readonly PlanBuilder _builder;
    private readonly PlanValidator _validator;
    private readonly ExecutionGraphBuilder _graphBuilder;
    private readonly ExecutionSupervisor _supervisor;
    private readonly IPlanRepository _planRepo;
    private readonly IPlanStepRepository _stepRepo;
    private readonly IPlanDependencyRepository _depRepo;
    private readonly IPlanExecutionLogRepository _logRepo;
    private readonly IAgentOrchestrator _orchestrator;
    private readonly ILogger<PlannerEngine> _logger;

    public PlannerEngine(
        PlanBuilder builder,
        PlanValidator validator,
        ExecutionGraphBuilder graphBuilder,
        ExecutionSupervisor supervisor,
        IPlanRepository planRepo,
        IPlanStepRepository stepRepo,
        IPlanDependencyRepository depRepo,
        IPlanExecutionLogRepository logRepo,
        IAgentOrchestrator orchestrator,
        ILogger<PlannerEngine> logger)
    {
        _builder = builder;
        _validator = validator;
        _graphBuilder = graphBuilder;
        _supervisor = supervisor;
        _planRepo = planRepo;
        _stepRepo = stepRepo;
        _depRepo = depRepo;
        _logRepo = logRepo;
        _orchestrator = orchestrator;
        _logger = logger;
    }

    public async Task<Plan> GenerarPlanAsync(string objetivo, int idUsuario, CancellationToken ct = default)
    {
        var plan = await _builder.ConstruirAsync(objetivo, idUsuario, ct);

        // Persistencia en UNA sola unidad: el Plan ya trae Pasos y Dependencias como
        // propiedades de navegación, así que un solo SaveChanges resuelve las FKs hijas.
        // NO se debe re-hacer AddAsync sobre los pasos (ya están trackeados por el contexto
        // al guardar el plan) — eso dispara "cannot be tracked" / FK violation.
        plan = await _planRepo.AddAsync(plan, ct);

        await RegistrarLogAsync(plan.IdPlan, null, "PlanGenerado",
            $"Plan #{plan.IdPlan} generado con {plan.Pasos.Count} paso(s). Requiere aprobación: {plan.RequiereAprobacion}.", ct);
        return plan;
    }

    public async Task<ResultadoValidacionPlan> ValidarPlanAsync(Plan plan, CancellationToken ct = default)
    {
        var resultado = await _validator.ValidarAsync(plan, ct);
        plan.Estado = resultado.Valido ? "Validado" : "Borrador";
        await _planRepo.UpdateAsync(plan, ct);
        await RegistrarLogAsync(plan.IdPlan, null, "PlanValidado",
            resultado.Valido ? "Plan válido." : $"Plan inválido: {string.Join("; ", resultado.Errores)}", ct);
        return resultado;
    }

    public ExecutionGraph ConstruirGrafo(Plan plan) => _graphBuilder.Construir(plan);

    public async Task<AgentExecutionResult> EjecutarPlanAsync(int idPlan, CancellationToken ct = default)
    {
        var plan = await _planRepo.GetByIdAsync(idPlan, ct)
                   ?? throw new InvalidOperationException($"Plan {idPlan} no encontrado.");

        // Regla 1: validar antes de ejecutar.
        var validacion = await ValidarPlanAsync(plan, ct);
        if (!validacion.Valido)
            throw new InvalidOperationException("El plan no pasó la validación: " + string.Join("; ", validacion.Errores));

        // Aprobación opcional (Regla de negocio 12): si requiere aprobación y no está aprobado, no ejecuta.
        if (plan.RequiereAprobacion && !plan.Aprobado)
            throw new InvalidOperationException("El plan requiere aprobación humana antes de ejecutarse.");

        // Regla 4: la ejecución pasa SIEMPRE por el Orchestrator.
        await _supervisor.IniciarAsync(plan, ct);

        var principal = plan.Pasos.FirstOrDefault(p => p.Tipo == "Agent") ?? plan.Pasos.First();
        var request = new AgentRequest
        {
            IdUsuario = plan.IdUsuario,
            IdAgentePrincipal = principal.IdAsistente ?? 1008,
            Pregunta = plan.Objetivo,
            PermitirColaboracion = true
        };

        // Ejecuta el grafo a través del Orchestrator (fire-and-forget interno del propio Orchestrator).
        var resultado = await _orchestrator.ExecuteAsync(request, ct);
        plan.IdExecution = resultado.IdExecution.ToString();
        await _planRepo.UpdateAsync(plan, ct);
        await RegistrarLogAsync(plan.IdPlan, null, "PlanEjecutado",
            $"Plan delegado al Agent Orchestrator. IdExecution={resultado.IdExecution}.", ct);

        await _supervisor.FinalizarAsync(plan, resultado.Exitoso, resultado.Error, ct);
        return resultado;
    }

    public async Task<SimulacionPlan> SimularAsync(int idPlan, CancellationToken cancellationToken = default)
    {
        var plan = await _planRepo.GetByIdAsync(idPlan, cancellationToken)
                   ?? throw new InvalidOperationException($"Plan {idPlan} no encontrado.");

        // Validación en seco (Regla 1 / Actividad 3) — sin ejecutar nada.
        var validacion = await _validator.ValidarAsync(plan, cancellationToken);

        // Predicción: participantes (agentes únicos) y herramientas que intervendrían.
        // El nombre del agente ya quedó registrado en PlanStep.Nombre por el Plan Builder.
        var participantes = plan.Pasos
            .Where(p => p.IdAsistente.HasValue)
            .Select(p => p.Nombre ?? $"Agente {p.IdAsistente}")
            .Distinct()
            .ToList();
        var herramientas = plan.Pasos
            .Where(p => !string.IsNullOrWhiteSpace(p.CodigoHerramienta))
            .Select(p => p.CodigoHerramienta!)
            .Distinct()
            .ToList();

        // Estimación simple en CPU (~60 s por paso de agente/herramienta, DeepSeek-r1:7b).
        int tiempoEstimado = plan.Pasos.Count * 60;

        return new SimulacionPlan
        {
            Plan = plan,
            Validacion = validacion,
            Participantes = participantes,
            Herramientas = herramientas,
            TiempoEstimadoSegundos = tiempoEstimado
        };
    }

    public async Task RegistrarLogAsync(int idPlan, int? idStep, string evento, string? detalle, CancellationToken ct = default)
        => await _logRepo.AddAsync(new PlanExecutionLog
        {
            IdPlan = idPlan,
            IdStep = idStep,
            Evento = evento,
            Detalle = detalle,
            Fecha = DateTime.UtcNow
        }, ct);
}
