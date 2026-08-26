using System;

namespace Asistente.Domain.Entities;

/// <summary>
/// Tarea programada mediante expresión Cron, ejecutada por Quartz.NET (ETAPA 13 - Actividad 5).
/// Asocia la tarea a un Workflow que se ejecuta de forma periódica y desacoplada.
/// </summary>
public class TareaProgramada
{
    public int IdTarea { get; set; }
    public string Nombre { get; set; } = string.Empty;

    /// <summary>Expresión Cron (formato Quartz, 6-7 campos). Ej: "0 0 2 * * ?" = cada día a las 2:00.</summary>
    public string ExpresionCron { get; set; } = string.Empty;

    public int IdWorkflow { get; set; }
    public bool Activa { get; set; } = true;

    public DateTime? UltimaEjecucion { get; set; }
    public DateTime? ProximaEjecucion { get; set; }

    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    public int UsuarioCreacion { get; set; } = 1;

    // Navegación
    public Workflow? Workflow { get; set; }
}
