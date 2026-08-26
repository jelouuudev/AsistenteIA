using System;
using System.Collections.Generic;

namespace Asistente.Domain.Entities;

/// <summary>
/// Ejecución de una solicitud a través del Agent Orchestrator (ETAPA 17).
/// Representa el ciclo de vida completo de una petición que puede involucrar
/// a varios agentes colaborando bajo coordinación centralizada.
/// </summary>
public class AgentExecution
{
    public int IdExecution { get; set; }
    public int IdUsuario { get; set; }
    public int IdAgentePrincipal { get; set; }
    public string Pregunta { get; set; } = string.Empty;
    public DateTime FechaInicio { get; set; } = DateTime.UtcNow;
    public DateTime? FechaFin { get; set; }
    public string Estado { get; set; } = "EnProceso"; // EnProceso | Completado | Cancelado | Error
    public long? TiempoTotalMs { get; set; }

    // Métricas de colaboración (para dashboard y auditoría)
    public int CantidadAgentes { get; set; }
    public int ProfundidadAlcanzada { get; set; }
    public int HerramientasUtilizadas { get; set; }
    public string? RespuestaFinal { get; set; }
    public string? Error { get; set; }

    // Navegación
    public Asistente? AgentePrincipal { get; set; }
    public ICollection<AgentExecutionStep> Pasos { get; set; } = new List<AgentExecutionStep>();
    public ICollection<AgentExecutionTrace> Trazas { get; set; } = new List<AgentExecutionTrace>();
}
