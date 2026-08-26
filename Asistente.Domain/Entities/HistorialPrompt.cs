namespace Asistente.Domain.Entities;

public class HistorialPrompt
{
    public int IdHistorial { get; set; }
    public int IdPrompt { get; set; }
    public int Version { get; set; }
    public string Contenido { get; set; } = string.Empty;
    public DateTime FechaModificacion { get; set; } = DateTime.UtcNow;
    public string UsuarioModificacion { get; set; } = string.Empty;
    public string? MotivoCambio { get; set; }

    // Navigation properties
    public PromptSistema? Prompt { get; set; }
}
