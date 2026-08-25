namespace Asistente.Domain.Entities;

/// <summary>
/// Representa un plan de ejecución generado por el Planner Engine (ETAPA 18).
/// Un plan es la representación estructurada de las acciones necesarias para
/// resolver una solicitud compleja expresada en lenguaje natural.
/// </summary>
public class Plan
{
    public int IdPlan { get; set; }
    public int IdUsuario { get; set; }
    public string Objetivo { get; set; } = string.Empty;
    public string Estado { get; set; } = "Borrador"; // Borrador, Validado, EnEjecucion, Completado, Fallido, Cancelado
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    public DateTime? FechaInicio { get; set; }
    public DateTime? FechaFin { get; set; }
    public long? TiempoTotalMs { get; set; }
    public int Version { get; set; } = 1;
    public string? Razonamiento { get; set; } // registro del razonamiento operativo del Planner
    public bool RequiereAprobacion { get; set; }
    public bool Aprobado { get; set; }
    public string? IdExecution { get; set; } // vincula el plan con la ejecución del Orchestrator (ETAPA 17)

    public ICollection<PlanStep> Pasos { get; set; } = new List<PlanStep>();
    public ICollection<PlanDependency> Dependencias { get; set; } = new List<PlanDependency>();
    public ICollection<PlanExecutionLog> Logs { get; set; } = new List<PlanExecutionLog>();
}
