namespace Asistente.Domain.Entities;

/// <summary>
/// Log de auditoría de la ejecución de un plan (ETAPA 18 - Regla 5 y Sección 13).
/// Registra objetivo, pasos, dependencias, herramientas, agentes, resultado, tiempo y errores.
/// </summary>
public class PlanExecutionLog
{
    public int IdLog { get; set; }
    public int IdPlan { get; set; }
    public int? IdStep { get; set; }
    public string Evento { get; set; } = string.Empty;
    public string? Detalle { get; set; }
    public DateTime Fecha { get; set; } = DateTime.UtcNow;

    public Plan? Plan { get; set; }
}
