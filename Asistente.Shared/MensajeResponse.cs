namespace Asistente.Shared;

public class MensajeResponse
{
    public int IdConversacion { get; set; }
    public string Respuesta { get; set; } = string.Empty;
    public long TiempoRespuestaMs { get; set; }
    public bool Exitoso { get; set; }
    public string? Error { get; set; }
}
