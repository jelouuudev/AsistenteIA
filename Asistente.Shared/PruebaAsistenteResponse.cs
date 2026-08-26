namespace Asistente.Shared;

public class PruebaAsistenteResponse
{
    public bool Exitoso { get; set; }
    public string? PromptGenerado { get; set; }
    public string? Respuesta { get; set; }
    public string? Error { get; set; }
    public long TiempoRespuestaMs { get; set; }
}
