namespace Asistente.Shared;

public class LoginRequest
{
    public string Usuario { get; set; } = string.Empty;
    public string Contrasena { get; set; } = string.Empty;
    public string? DireccionIP { get; set; }
    public string? Navegador { get; set; }
}
