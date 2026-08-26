using System.Collections.Generic;

namespace Asistente.Shared;

public class CrearUsuarioRequest
{
    public string UsuarioNombre { get; set; } = string.Empty;
    public string Nombres { get; set; } = string.Empty;
    public string Apellidos { get; set; } = string.Empty;
    public string Correo { get; set; } = string.Empty;
    public string Contrasena { get; set; } = string.Empty;
    public List<string> Roles { get; set; } = new();
}
