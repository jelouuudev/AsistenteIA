using System.Threading;
using System.Threading.Tasks;
using Asistente.Domain.Entities;
using Asistente.Application.Orchestrator;

namespace Asistente.Application.Interfaces;

/// <summary>
/// Planner Engine (ETAPA 18). Punto de entrada del componente: comprende objetivos,
/// identifica tareas, crea el plan, determina riesgos, solicita aprobaciones y entrega
/// un plan ejecutable. Gobierna el Execution Graph validado; cada paso lo ejecuta el
/// Agent Orchestrator sin re-seleccionar (fuente única de verdad, B-01).
/// </summary>
public interface IPlannerEngine
{
    /// <summary>Genera un plan estructurado a partir de una solicitud en lenguaje natural.</summary>
    Task<Plan> GenerarPlanAsync(string objetivo, int idUsuario, CancellationToken cancellationToken = default);

    /// <summary>Valida permisos, herramientas, agentes, restricciones y riesgos (Regla 1).</summary>
    Task<ResultadoValidacionPlan> ValidarPlanAsync(Plan plan, CancellationToken cancellationToken = default);

    /// <summary>Ejecuta el plan desde su Execution Graph validado (cada paso vía Orchestrator).</summary>
    Task<AgentExecutionResult> EjecutarPlanAsync(int idPlan, CancellationToken cancellationToken = default);

    /// <summary>Continúa un plan pausado cuya aprobación ya fue resuelta (reanudación por evento).</summary>
    Task ContinuarPlanAprobadoAsync(int idPlan, CancellationToken cancellationToken = default);

    /// <summary>Construye el grafo de ejecución (DAG) a partir de un plan ya validado.</summary>
    ExecutionGraph ConstruirGrafo(Plan plan);

    /// <summary>Simulación en seco (Actividad 5): valida el plan SIN ejecutarlo y predice
    /// participantes, herramientas y riesgos.</summary>
    Task<SimulacionPlan> SimularAsync(int idPlan, CancellationToken cancellationToken = default);

    /// <summary>Registra un evento en el log de auditoría del plan (Regla 5 / Sección 13).</summary>
    Task RegistrarLogAsync(int idPlan, int? idStep, string evento, string? detalle, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancelación real (B-03): invoca Cancel() sobre el trabajo activo del plan
    /// (si existe) y marca el estado. Devuelve false si el plan no existe.
    /// </summary>
    Task<bool> CancelarEjecucionAsync(int idPlan, CancellationToken cancellationToken = default);
}

/// <summary>Resultado de la simulación en seco: validación + predicción de ejecución.</summary>
public class SimulacionPlan
{
    public Plan Plan { get; set; } = new();
    public ResultadoValidacionPlan Validacion { get; set; } = new();
    public List<string> Participantes { get; set; } = new();
    public List<string> Herramientas { get; set; } = new();
    public int TiempoEstimadoSegundos { get; set; }
}

/// <summary>Resultado de la validación de un plan por el Plan Validator.</summary>
public class ResultadoValidacionPlan
{
    public bool Valido { get; set; }
    public List<string> Errores { get; set; } = new();
    public List<string> Advertencias { get; set; } = new();
    public List<string> Riesgos { get; set; } = new();
}
