using System;
using System.Collections.Generic;

namespace Asistente.Domain.Entities;

/// <summary>
/// Registro de auditoría de una ejecución completa de un flujo de trabajo.
/// </summary>
public class WorkflowEjecucion
{
    public int IdEjecucion { get; set; }
    public int IdWorkflow { get; set; }
    public int IdUsuario { get; set; }
    public int? IdAsistente { get; set; }
    public DateTime FechaInicio { get; set; } = DateTime.UtcNow;
    public DateTime? FechaFin { get; set; }
    public string Estado { get; set; } = "EnProceso";
    public long? TiempoTotalMs { get; set; }
    public string? ResultadoFinal { get; set; }
    public bool Confirmado { get; set; }

    public Workflow? Workflow { get; set; }
    public ICollection<WorkflowPasoEjecucion> PasosEjecucion { get; set; } = new List<WorkflowPasoEjecucion>();
}
