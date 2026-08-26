namespace Asistente.Shared;

public class PruebaAsistenteRequest
{
    public int IdAsistente { get; set; }
    public int? IdPrompt { get; set; }
    public string Mensaje { get; set; } = string.Empty;
}
