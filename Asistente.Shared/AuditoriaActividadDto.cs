using System;

namespace Asistente.Shared;

public class AuditoriaActividadDto
{
    public int IdActividad { get; set; }
    public int IdUsuario { get; set; }
    public string UsuarioNombre { get; set; } = string.Empty;
    public DateTime FechaHora { get; set; }
    public string Modulo { get; set; } = string.Empty;
    public string Accion { get; set; } = string.Empty;
    public string TipoOperacion { get; set; } = string.Empty;
    public string Resultado { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public string? DireccionIP { get; set; }
}
