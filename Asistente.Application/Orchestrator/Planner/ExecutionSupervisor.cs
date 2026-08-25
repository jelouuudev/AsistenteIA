using System;
using System.Threading;
using System.Threading.Tasks;
using Asistente.Application.Interfaces;
using Asistente.Domain.Entities;

namespace Asistente.Application.Orchestrator.Planner;

/// <summary>
/// Execution Supervisor (ETAPA 18, Actividad 7). Monitorea estado, duración, errores y
/// reintentos de un plan en ejecución. Aplica la política de reintentos configurada.
/// </summary>
public class ExecutionSupervisor
{
    private readonly IPlanRepository _planRepo;
    private readonly IPlanStepRepository _stepRepo;
    private readonly IPlanExecutionLogRepository _logRepo;
    private readonly int _maxReintentos;
    private readonly int _tiempoEntreIntentosMs;

    public ExecutionSupervisor(
        IPlanRepository planRepo,
        IPlanStepRepository stepRepo,
        IPlanExecutionLogRepository logRepo,
        int maxReintentos = 2,
        int tiempoEntreIntentosMs = 5000)
    {
        _planRepo = planRepo;
        _stepRepo = stepRepo;
        _logRepo = logRepo;
        _maxReintentos = maxReintentos;
        _tiempoEntreIntentosMs = tiempoEntreIntentosMs;
    }

    /// <summary>Registra el inicio de la supervisión de un plan.</summary>
    public async Task IniciarAsync(Plan plan, CancellationToken ct)
    {
        plan.Estado = "EnEjecucion";
        plan.FechaInicio = DateTime.UtcNow;
        await _planRepo.UpdateAsync(plan, ct);
        await RegistrarAsync(plan.IdPlan, null, "SupervisionInicio",
            $"Supervisor activo. Máx reintentos={_maxReintentos}, intervalo={_tiempoEntreIntentosMs}ms.", ct);
    }

    /// <summary>Marca un paso como error y decide si reintenta (dentro del máximo configurado).</summary>
    public async Task<bool> ManejarFalloPasoAsync(Plan plan, PlanStep paso, string error, CancellationToken ct)
    {
        paso.Intentos++;
        if (paso.Intentos <= _maxReintentos)
        {
            paso.Estado = "Pendiente";
            await _stepRepo.UpdateAsync(paso, ct);
            await RegistrarAsync(plan.IdPlan, paso.IdStep, "Reintento",
                $"Paso '{paso.Nombre}' falló ({error}). Reintento {paso.Intentos}/{_maxReintentos}.", ct);
            await Task.Delay(_tiempoEntreIntentosMs, ct);
            return true; // reintentar
        }

        paso.Estado = "Error";
        paso.Resultado = error;
        await _stepRepo.UpdateAsync(paso, ct);
        await RegistrarAsync(plan.IdPlan, paso.IdStep, "PasoError",
            $"Paso '{paso.Nombre}' agotó reintentos: {error}.", ct);
        return false; // no reintentar
    }

    /// <summary>Finaliza la supervisión (éxito o fallo).</summary>
    public async Task FinalizarAsync(Plan plan, bool exitoso, string? motivo, CancellationToken ct)
    {
        plan.Estado = exitoso ? "Completado" : "Fallido";
        plan.FechaFin = DateTime.UtcNow;
        if (plan.FechaInicio.HasValue)
            plan.TiempoTotalMs = (long)(plan.FechaFin.Value - plan.FechaInicio.Value).TotalMilliseconds;
        await _planRepo.UpdateAsync(plan, ct);
        await RegistrarAsync(plan.IdPlan, null, exitoso ? "SupervisionFin" : "SupervisionFinConError",
            motivo ?? (exitoso ? "Plan completado." : "Plan finalizado con errores."), ct);
    }

    private async Task RegistrarAsync(int idPlan, int? idStep, string evento, string detalle, CancellationToken ct)
        => await _logRepo.AddAsync(new PlanExecutionLog
        {
            IdPlan = idPlan,
            IdStep = idStep,
            Evento = evento,
            Detalle = detalle,
            Fecha = DateTime.UtcNow
        }, ct);
}
