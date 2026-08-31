namespace Asistente.Shared;

public class MensajeRequest
{
    public int? IdConversacion { get; set; }
    public int? IdAsistente { get; set; }
    public string Mensaje { get; set; } = string.Empty;
    public int UsuarioPropietario { get; set; }

    /// <summary>
    /// ETAPA 17 (Híbrido): cuando es true, el Agent Orchestrator coordina la colaboración
    /// multi-agente para esta solicitud en lugar de resolverla con un solo agente.
    /// </summary>
    public bool PermitirColaboracionMultagente { get; set; }

    /// <summary>
    /// ETAPA 19.1: cuando es true, el ChatService NO aplica el short-circuit anti-alucinación
    /// y deja que el LLM genere la respuesta en lenguaje natural. Esto es necesario para que
    /// los pasos Agent del Planner muestren análisis/coordinación, no datos crudos de SQL.
    /// </summary>
    public bool EsEjecucionPlan { get; set; }
}
