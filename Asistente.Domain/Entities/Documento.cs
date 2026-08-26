using System;
using System.Collections.Generic;
using Asistente.Domain.Enums;

namespace Asistente.Domain.Entities;

public class Documento
{
    public int IdDocumento { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public int IdCategoria { get; set; }
    public int VersionActual { get; set; }
    public EstadoDocumento Estado { get; set; } = EstadoDocumento.Activo;
    public bool PendienteProcesamiento { get; set; } = true;
    public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;
    public int UsuarioRegistro { get; set; }

    // Navigation properties
    public CategoriaDocumento? Categoria { get; set; }
    public ICollection<DocumentoVersion> Versiones { get; set; } = new List<DocumentoVersion>();
    public ICollection<AuditoriaDocumental> Auditorias { get; set; } = new List<AuditoriaDocumental>();
    public ICollection<DocumentoFuente> DocumentosFuentes { get; set; } = new List<DocumentoFuente>();
}
