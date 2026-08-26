using System;
using System.Collections.Generic;

namespace Asistente.Shared;

public class UsuarioDto
{
    public int IdUsuario { get; set; }
    public string UsuarioNombre { get; set; } = string.Empty;
    public string Nombres { get; set; } = string.Empty;
    public string Apellidos { get; set; } = string.Empty;
    public string Correo { get; set; } = string.Empty;
    public bool Activo { get; set; }
    public DateTime FechaCreacion { get; set; }
    public DateTime? FechaUltimoAcceso { get; set; }
    public List<string> Roles { get; set; } = new();
}
