using System.Threading;
using System.Threading.Tasks;
using Asistente.Domain.Entities;
using Asistente.Application.Orchestrator;

namespace Asistente.Application.Interfaces;

/// <summary>
/// Planner Engine (ETAPA 18). Punto de entrada del componente: comprende objetivos,
/// identifica tareas, crea el plan, determina riesgos, solicita aprobaciones y entrega
/// un plan ejecutable. NUNCA ejecuta acciones directamente (Regla 3): delega en el
/// Agent Orchestrator (ETAPA 17).
/// </summary>
public interface IPlannerEngine
{
    /// <summary>Genera un plan estructurado a partir de una solicitud en lenguaje natural.</summary>
    Task<Plan> GenerarPlanAsync(string objetivo, int idUsuario, CancellationToken cancellationToken = default);

    /// <summary>Valida permisos, herramientas, agentes, restricciones y riesgos (Regla 1).</summary>
    Task<ResultadoValidacionPlan> ValidarPlanAsync(Plan plan, CancellationToken cancellationToken = default);

    /// <summary>Transforma el plan en un grafo ejecutable (DAG) y lo entrega al Orchestrator.</summary>
    Task<AgentExecutionResult> EjecutarPlanAsync(int idPlan, CancellationToken cancellationToken = default);

    /// <summary>Construye el grafo de ejecución (DAG) a partir de un plan ya validado.</summary>
    ExecutionGraph ConstruirGrafo(Plan plan);

    /// <summary>Registra un evento en el log de auditoría del plan (Regla 5 / Sección 13).</summary>
    Task RegistrarLogAsync(int idPlan, int? idStep, string evento, string? detalle, CancellationToken cancellationToken = default);
}

/// <summary>Resultado de la validación de un plan por el Plan Validator.</summary>
public class ResultadoValidacionPlan
{
    public bool Valido { get; set; }
    public List<string> Errores { get; set; } = new();
    public List<string> Advertencias { get; set; } = new();
    public List<string> Riesgos { get; set; } = new();
}
