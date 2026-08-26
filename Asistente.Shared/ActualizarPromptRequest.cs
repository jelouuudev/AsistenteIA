namespace Asistente.Shared;

public class ActualizarPromptRequest
{
    public string Nombre { get; set; } = string.Empty;
    public string Contenido { get; set; } = string.Empty;
    public bool Activo { get; set; }
    public string? MotivoCambio { get; set; }
}
