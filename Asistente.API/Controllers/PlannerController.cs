using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Asistente.Application.Interfaces;
using Asistente.Application.Orchestrator.Planner;
using Asistente.Domain.Entities;
using Asistente.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Asistente.API.Controllers;

[ApiController]
[Route("api/planner")]
[Authorize]
public class PlannerController : ControllerBase
{
    private readonly IPlannerEngine _planner;
    private readonly IPlanRepository _planRepo;
    private readonly IServiceScopeFactory _scopeFactory;

    public PlannerController(
        IPlannerEngine planner,
        IPlanRepository planRepo,
        IServiceScopeFactory scopeFactory)
    {
        _planner = planner;
        _planRepo = planRepo;
        _scopeFactory = scopeFactory;
    }

    private int UsuarioId()
        => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    /// <summary>Genera un plan a partir de una solicitud en lenguaje natural (Actividad 1, 2).</summary>
    [HttpPost("generar")]
    public async Task<ActionResult<PlanDto>> Generar([FromBody] GenerarPlanRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Objetivo))
            return BadRequest("Debe indicar el objetivo.");

        var plan = await _planner.GenerarPlanAsync(request.Objetivo, UsuarioId(), ct);
        return Ok(ToDto(plan));
    }

    /// <summary>Valida un plan antes de ejecutarlo (Regla 1, Actividad 3).</summary>
    [HttpPost("validar/{id:int}")]
    public async Task<ActionResult<ResultadoValidacionPlan>> Validar(int id, CancellationToken ct)
    {
        var plan = await _planRepo.GetByIdAsync(id, ct)
                   ?? (Plan?)null;
        if (plan == null) return NotFound();
        var resultado = await _planner.ValidarPlanAsync(plan, ct);
        return Ok(resultado);
    }

    /// <summary>Simula el plan (lo muestra sin ejecutarlo) para visualización previa (Actividad 5).</summary>
    [HttpGet("simular/{id:int}")]
    public async Task<ActionResult<SimulacionPlanDto>> Simular(int id, CancellationToken ct)
    {
        var plan = await _planRepo.GetByIdAsync(id, ct);
        if (plan == null) return NotFound();

        // Simulación en seco: valida SIN ejecutar (Regla 3) y predice participantes/herramientas.
        var simulacion = await _planner.SimularAsync(id, ct);
        var dto = new SimulacionPlanDto
        {
            Plan = ToDto(simulacion.Plan),
            Validacion = new ResultadoValidacionPlanDto
            {
                Valido = simulacion.Validacion.Valido,
                Errores = simulacion.Validacion.Errores,
                Advertencias = simulacion.Validacion.Advertencias,
                Riesgos = simulacion.Validacion.Riesgos
            },
            Participantes = simulacion.Participantes,
            Herramientas = simulacion.Herramientas,
            TiempoEstimadoSegundos = simulacion.TiempoEstimadoSegundos
        };
        return Ok(dto);
    }

    /// <summary>Ejecuta el plan delegándolo al Agent Orchestrator (Regla 4). Fire-and-forget:
    /// devuelve el IdPlan de inmediato; la ejecución (lenta en CPU) corre en segundo plano.</summary>
    [HttpPost("ejecutar/{id:int}")]
    public async Task<ActionResult<PlanDto>> Ejecutar(int id, CancellationToken ct)
    {
        var plan = await _planRepo.GetByIdAsync(id, ct);
        if (plan == null) return NotFound();

        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var engine = scope.ServiceProvider.GetRequiredService<IPlannerEngine>();
                await engine.EjecutarPlanAsync(id, CancellationToken.None);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR background planner (Plan {id}): {ex}");
            }
        });

        return Ok(ToDto(plan));
    }

    /// <summary>Aprueba un plan que requiere aprobación humana (Regla de negocio 12).</summary>
    [HttpPost("aprobar/{id:int}")]
    public async Task<ActionResult> Aprobar(int id, CancellationToken ct)
    {
        var plan = await _planRepo.GetByIdAsync(id, ct);
        if (plan == null) return NotFound();
        plan.Aprobado = true;
        await _planRepo.UpdateAsync(plan, ct);
        return Ok(new { aprobado = true });
    }

    /// <summary>Cancela un plan en ejecución (Actividad 8, Regla 6).</summary>
    [HttpPost("cancelar/{id:int}")]
    public async Task<ActionResult> Cancelar(int id, CancellationToken ct)
    {
        var plan = await _planRepo.GetByIdAsync(id, ct);
        if (plan == null) return NotFound();
        plan.Estado = "Cancelado";
        plan.FechaFin = DateTime.UtcNow;
        await _planRepo.UpdateAsync(plan, ct);
        await _planner.RegistrarLogAsync(id, null, "PlanCancelado", "Cancelado por el usuario.", ct);
        return Ok(new { cancelado = true });
    }

    /// <summary>Dashboard de planes (Actividad 10): activos, finalizados, fallidos, tiempo promedio.</summary>
    [HttpGet("dashboard")]
    public async Task<ActionResult<PlannerDashboardDto>> Dashboard(CancellationToken ct)
    {
        var todos = await _planRepo.GetRecentAsync(200, ct);
        var activos = todos.Count(p => p.Estado == "EnEjecucion");
        var finalizados = todos.Count(p => p.Estado == "Completado");
        var fallidos = todos.Count(p => p.Estado == "Fallido" || p.Estado == "Cancelado");
        var conTiempo = todos.Where(p => p.TiempoTotalMs.HasValue).ToList();
        var promedio = conTiempo.Any() ? (long)conTiempo.Average(p => p.TiempoTotalMs!.Value) : 0;

        return Ok(new PlannerDashboardDto
        {
            Total = todos.Count,
            Activos = activos,
            Finalizados = finalizados,
            Fallidos = fallidos,
            TiempoPromedioMs = promedio,
            Planes = todos.Select(ToDto).ToList()
        });
    }

    /// <summary>Detalle de un plan (pasos, dependencias, logs, ejecución vinculada).</summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<PlanDto>> Get(int id, CancellationToken ct)
    {
        var plan = await _planRepo.GetByIdAsync(id, ct);
        if (plan == null) return NotFound();

        // Sincroniza el progreso fino del Orchestrator en los PlanStep (para que el DAG
        // refleje los estados reales al recargar un plan ya ejecutado/cancelado).
        if (!string.IsNullOrWhiteSpace(plan.IdExecution)
            && (plan.Estado == "Completado" || plan.Estado == "Fallido" || plan.Estado == "Cancelado"))
        {
            try { await _planner.SincronizarPlanStepsAsync(id, plan.IdExecution, ct); }
            catch { /* no bloquea la lectura si la sincronización falla */ }
            plan = await _planRepo.GetByIdAsync(id, ct) ?? plan;
        }

        return Ok(ToDto(plan));
    }

    private static Asistente.Shared.PlanDto ToDto(Plan plan) => new()
    {
        IdPlan = plan.IdPlan,
        Objetivo = plan.Objetivo,
        Estado = plan.Estado,
        RequiereAprobacion = plan.RequiereAprobacion,
        Aprobado = plan.Aprobado,
        IdExecution = plan.IdExecution,
        TiempoTotalMs = plan.TiempoTotalMs,
        Razonamiento = plan.Razonamiento,
        Pasos = plan.Pasos.OrderBy(s => s.Orden).Select(s => new Asistente.Shared.PlanStepDto
        {
            IdStep = s.IdStep,
            Orden = s.Orden,
            Tipo = s.Tipo,
            Nombre = s.Nombre,
            Descripcion = s.Descripcion,
            Estado = s.Estado,
            Resultado = s.Resultado,
            IdAsistente = s.IdAsistente,
            CodigoHerramienta = s.CodigoHerramienta,
            Intentos = s.Intentos
        }).ToList(),
        Dependencias = plan.Dependencias.Select(d => new Asistente.Shared.PlanDepDto
        {
            StepOrigen = d.StepOrigen,
            StepDestino = d.StepDestino
        }).ToList(),
        Logs = plan.Logs.OrderBy(l => l.Fecha).Select(l => new Asistente.Shared.PlanLogDto
        {
            Evento = l.Evento,
            Detalle = l.Detalle,
            Fecha = l.Fecha
        }).ToList()
    };
}

public class GenerarPlanRequest { public string Objetivo { get; set; } = string.Empty; }
