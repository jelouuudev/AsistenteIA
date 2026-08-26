namespace Asistente.Shared;

public class PromptSistemaDto
{
    public int IdPrompt { get; set; }
    public int IdAsistente { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Contenido { get; set; } = string.Empty;
    public int Version { get; set; }
    public bool Activo { get; set; }
    public DateTime FechaCreacion { get; set; }
    public string UsuarioCreacion { get; set; } = string.Empty;
    public string? AsistenteNombre { get; set; }
}
