using System.Collections.Generic;

namespace Asistente.Application.Orchestrator;

/// <summary>
/// Solicitud de ejecución al Agent Orchestrator (RF §7 / §8).
/// </summary>
public class AgentRequest
{
    public int IdUsuario { get; set; }
    public int IdAgentePrincipal { get; set; }
    public string Pregunta { get; set; } = string.Empty;
    /// <summary>Agentes sugeridos explícitamente por el usuario (opcional).</summary>
    public List<int>? AgentesSugeridos { get; set; }
    /// <summary>True cuando se activa el checkbox "Permitir colaboración multi-agente".</summary>
    public bool PermitirColaboracion { get; set; } = true;
    public string? IdConversacion { get; set; }
    /// <summary>
    /// ETAPA 19.3: contexto de resultados de pasos anteriores (Tool) para que los
    /// pasos Agent tengan acceso a los datos reales y no inventen valores.
    /// </summary>
    public string? ContextoPrevio { get; set; }
}

/// <summary>
/// Resultado consolidado de una ejecución orquestada (RF §7).
/// </summary>
public class AgentExecutionResult
{
    public int IdExecution { get; set; }
    public bool Exitoso { get; set; }
    public string? RespuestaFinal { get; set; }
    public string Estado { get; set; } = "Completado";
    public long TiempoTotalMs { get; set; }
    public int CantidadAgentes { get; set; }
    public int ProfundidadAlcanzada { get; set; }
    public List<string> AgentesParticipantes { get; set; } = new();
    public List<AgentStepResult> Pasos { get; set; } = new();
    public List<ExecutionTraceDto> Trazas { get; set; } = new();
    public string? Error { get; set; }
}

public class AgentStepResult
{
    public int Orden { get; set; }
    public int IdAgente { get; set; }
    public string NombreAgente { get; set; } = string.Empty;
    public string Accion { get; set; } = string.Empty;
    public string? Resultado { get; set; }
    public long TiempoMs { get; set; }
    public string Estado { get; set; } = "Completado";
    public List<int> Dependencias { get; set; } = new();
}

public class ExecutionTraceDto
{
    public string Evento { get; set; } = string.Empty;
    public string? Detalle { get; set; }
    public System.DateTime FechaHora { get; set; }
}

/// <summary>
/// Candidato a agente colaborador seleccionado por el Agent Selector (RF §7 / Actividad 2).
/// </summary>
public class AgentCandidate
{
    public int IdAgente { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Objetivo { get; set; } = string.Empty;
    /// <summary>Rol que jugará en el grafo: "SQL", "RAG", "Reporte", "Principal", etc.</summary>
    public string Rol { get; set; } = "Colaborador";
    public int Prioridad { get; set; } = 100;
    /// <summary>Ids de candidatos de los que depende (ejecución secuencial).</summary>
    public List<int> DependeDe { get; set; } = new();
    public double Puntuacion { get; set; }
}
