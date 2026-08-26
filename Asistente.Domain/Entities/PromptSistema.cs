namespace Asistente.Domain.Entities;

public class PromptSistema
{
    public int IdPrompt { get; set; }
    public int IdAsistente { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Contenido { get; set; } = string.Empty;
    public int Version { get; set; } = 1;
    public bool Activo { get; set; } = true;
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    public string UsuarioCreacion { get; set; } = string.Empty;

    // Navigation properties
    public Asistente? Asistente { get; set; }
    public ICollection<HistorialPrompt> Historial { get; set; } = new List<HistorialPrompt>();
}
