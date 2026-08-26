namespace Asistente.Shared;

public class HistorialPromptDto
{
    public int IdHistorial { get; set; }
    public int IdPrompt { get; set; }
    public int Version { get; set; }
    public string Contenido { get; set; } = string.Empty;
    public DateTime FechaModificacion { get; set; }
    public string UsuarioModificacion { get; set; } = string.Empty;
    public string? MotivoCambio { get; set; }
    public string? PromptNombre { get; set; }
}
