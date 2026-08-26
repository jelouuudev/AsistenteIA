using System;
using System.Collections.Generic;

namespace Asistente.Domain.Entities;

public class Usuario
{
    public int IdUsuario { get; set; }
    public string UsuarioNombre { get; set; } = string.Empty; // Mapped to 'Usuario' in database
    public string Nombres { get; set; } = string.Empty;
    public string Apellidos { get; set; } = string.Empty;
    public string Correo { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public bool Activo { get; set; } = true;
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    public DateTime? FechaUltimoAcceso { get; set; }

    // Navigation properties
    public ICollection<UsuarioRol> UsuarioRoles { get; set; } = new List<UsuarioRol>();
    public ICollection<AuditoriaSesion> AuditoriasSesion { get; set; } = new List<AuditoriaSesion>();
    public ICollection<AuditoriaActividad> AuditoriasActividad { get; set; } = new List<AuditoriaActividad>();
}
