using System;

namespace Asistente.Shared;

public class AuditoriaSesionDto
{
    public int IdSesion { get; set; }
    public int IdUsuario { get; set; }
    public string UsuarioNombre { get; set; } = string.Empty;
    public DateTime FechaInicio { get; set; }
    public DateTime? FechaFin { get; set; }
    public string? DireccionIP { get; set; }
    public string? Navegador { get; set; }
    public string Estado { get; set; } = string.Empty;
}
