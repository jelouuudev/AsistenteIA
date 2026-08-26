using System;

namespace Asistente.Domain.Entities;

public class AuditoriaSesion
{
    public int IdSesion { get; set; }
    public int IdUsuario { get; set; }
    public Usuario? Usuario { get; set; }
    public DateTime FechaInicio { get; set; } = DateTime.UtcNow;
    public DateTime? FechaFin { get; set; }
    public string? DireccionIP { get; set; }
    public string? Navegador { get; set; }
    public string Estado { get; set; } = string.Empty; // e.g. "Exitoso", "Fallido", "Activo", "Cerrado"
}
