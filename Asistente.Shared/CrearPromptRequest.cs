namespace Asistente.Shared;

public class CrearPromptRequest
{
    public int IdAsistente { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Contenido { get; set; } = string.Empty;
    public string UsuarioCreacion { get; set; } = string.Empty;
}
