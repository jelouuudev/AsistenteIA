using System;

namespace Asistente.Domain.Entities;

/// <summary>
/// Trazabilidad completa de un evento dentro de una ejecución del Orchestrator
/// (ETAPA 17, §6 / Actividad 13 - Visualización de trazas).
/// Permite reconstruir el recorrido exacto de cómo se construyó una respuesta.
/// </summary>
public class AgentExecutionTrace
{
    public int IdTrace { get; set; }
    public int IdExecution { get; set; }
    public string Evento { get; set; } = string.Empty; // Ej: "SeleccionAgente", "EjecucionNodo", "Consolidacion"
    public string? Detalle { get; set; }
    public DateTime FechaHora { get; set; } = DateTime.UtcNow;

    public AgentExecution? Ejecucion { get; set; }
}
