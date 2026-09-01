using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Asistente.Application.Orchestrator;
using Asistente.Domain.Entities;

namespace Asistente.Application.Interfaces;

/// <summary>Componente central que coordina la colaboración entre agentes (RF §7).</summary>
public interface IAgentOrchestrator
{
    Task<AgentExecutionResult> ExecuteAsync(AgentRequest request, CancellationToken cancellationToken = default);
    /// <summary>Crea la ejecución (EnProceso) y devuelve su IdExecution de inmediato.</summary>
    Task<int> IniciarAsync(AgentRequest request, CancellationToken cancellationToken = default);
    /// <summary>Ejecuta el Execution Graph en segundo plano para una ejecución ya creada.</summary>
    Task EjecutarGrafoAsync(int idExecution, AgentRequest request, CancellationToken cancellationToken = default);
}

/// <summary>Selecciona agentes colaboradores según tipo, capacidades, permisos, prioridad y reglas (RF Actividad 2).</summary>
public interface IAgentSelector
{
    Task<IEnumerable<AgentCandidate>> SelectAgentsAsync(AgentRequest request, CancellationToken cancellationToken = default);
}

/// <summary>Consolida los resultados parciales en una única respuesta coherente (RF Actividad 7).</summary>
public interface IResponseAggregator
{
    Task<string> BuildFinalResponseAsync(AgentExecution execution, CancellationToken cancellationToken = default);
}

/// <summary>Gestiona el contexto compartido autorizado entre agentes (RF Actividad 4).</summary>
public interface IContextManager
{
    /// <summary>Construye el contexto autorizado para un agente dado, a partir del contexto global y las reglas.</summary>
    Task<AgentContext> BuildContextForAgentAsync(int idAgente, SharedContext contextoGlobal, CancellationToken cancellationToken = default);
    bool PuedeCompartir(int idAgenteOrigen, int idAgenteDestino);
}

public class SharedContext
{
    public int IdUsuario { get; set; }
    public string PreguntaOriginal { get; set; } = string.Empty;
    public List<ContextoParcial> ResultadosPrevios { get; set; } = new();
}

public class ContextoParcial
{
    public int IdAgente { get; set; }
    public string NombreAgente { get; set; } = string.Empty;
    public string Contenido { get; set; } = string.Empty;
    public List<string> Herramientas { get; set; } = new();
    public List<string> Fuentes { get; set; } = new();
}

public class AgentContext
{
    public int IdAgente { get; set; }
    public string PreguntaAsignada { get; set; } = string.Empty;
    public List<ContextoParcial> ContextoAutorizado { get; set; } = new();
    public List<string> HerramientasDisponibles { get; set; } = new();
    /// <summary>
    /// ETAPA 19.3: contexto completo de pasos anteriores (Planner Engine) para inyectar al LLM.
    /// </summary>
    public string? ContextoPrevio { get; set; }
}
