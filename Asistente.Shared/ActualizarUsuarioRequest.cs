using System.Collections.Generic;

namespace Asistente.Shared;

public class ActualizarUsuarioRequest
{
    public string Nombres { get; set; } = string.Empty;
    public string Apellidos { get; set; } = string.Empty;
    public string Correo { get; set; } = string.Empty;
    public bool Activo { get; set; }
    public List<string> Roles { get; set; } = new();
}
