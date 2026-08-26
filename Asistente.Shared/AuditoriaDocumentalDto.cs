using System;

namespace Asistente.Shared;

public class AuditoriaDocumentalDto
{
    public int IdAuditoria { get; set; }
    public int IdDocumento { get; set; }
    public string DocumentoNombre { get; set; } = string.Empty;
    public int? IdVersion { get; set; }
    public string Accion { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public int UsuarioId { get; set; }
    public DateTime FechaAccion { get; set; }
    public string? DireccionIP { get; set; }
}
