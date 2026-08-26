using System;

namespace Asistente.Domain.Entities;

public class AuditoriaDocumental
{
    public int IdAuditoria { get; set; }
    public int IdDocumento { get; set; }
    public int? IdVersion { get; set; }
    public string Accion { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public int UsuarioId { get; set; }
    public DateTime FechaAccion { get; set; } = DateTime.UtcNow;
    public string? DireccionIP { get; set; }

    // Navigation properties
    public Documento? Documento { get; set; }
}
