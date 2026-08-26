using System;
using System.Collections.Generic;
using Asistente.Domain.Enums;

namespace Asistente.Domain.Entities;

public class FuenteConocimiento
{
    public int IdFuente { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Codigo { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public TipoFuente Tipo { get; set; } = TipoFuente.Manual;
    public bool Activo { get; set; } = true;
    public int Prioridad { get; set; } = 5;
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    public int UsuarioCreacion { get; set; }

    public ICollection<AsistenteFuente> AsistentesFuentes { get; set; } = new List<AsistenteFuente>();
    public ICollection<DocumentoFuente> DocumentosFuentes { get; set; } = new List<DocumentoFuente>();
}
