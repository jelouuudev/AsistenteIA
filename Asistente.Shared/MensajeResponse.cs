using System.Collections.Generic;

namespace Asistente.Shared;

public class MensajeResponse
{
    public int IdConversacion { get; set; }
    public string Respuesta { get; set; } = string.Empty;
    public long TiempoRespuestaMs { get; set; }
    public bool Exitoso { get; set; }
    public string? Error { get; set; }
    public string? TituloGenerado { get; set; }
    public long TiempoConstruccionContextoMs { get; set; }
    public int CantidadMensajesContexto { get; set; }
    public List<ReferenciaDocumentalDto> ReferenciasDocumentales { get; set; } = new();
    public List<HerramientaUsoChatDto> HerramientasUsadas { get; set; } = new();
    public List<WorkflowUsoChatDto> WorkflowsUsados { get; set; } = new();
    public bool RequiereConfirmacionWorkflow { get; set; }
    public string? MensajeConfirmacionWorkflow { get; set; }
    public int? IdEjecucionWorkflowPendiente { get; set; }
}
