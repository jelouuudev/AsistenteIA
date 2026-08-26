namespace Asistente.Shared;

public class LoginResponse
{
    public bool Exitoso { get; set; }
    public string? Error { get; set; }
    public UsuarioDto? Usuario { get; set; }
    public int? IdSesion { get; set; }
    public string? Token { get; set; }
}
