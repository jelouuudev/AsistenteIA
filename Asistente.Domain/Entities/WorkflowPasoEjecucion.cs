using System;

namespace Asistente.Domain.Entities;

/// <summary>
/// Auditoría de la ejecución de un paso específico dentro de un flujo de trabajo.
/// </summary>
public class WorkflowPasoEjecucion
{
    public int IdPasoEjecucion { get; set; }
    public int IdEjecucion { get; set; }
    public int IdPaso { get; set; }
    public DateTime FechaInicio { get; set; } = DateTime.UtcNow;
    public DateTime? FechaFin { get; set; }
    public string? Resultado { get; set; }
    public string Estado { get; set; } = "EnProceso";
    public string? Observaciones { get; set; }

    public WorkflowEjecucion? WorkflowEjecucion { get; set; }
}
