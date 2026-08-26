using System;

namespace Asistente.Domain.Entities;

public class DocumentoVersion
{
    public int IdVersion { get; set; }
    public int IdDocumento { get; set; }
    public int NumeroVersion { get; set; }
    public string NombreArchivo { get; set; } = string.Empty;
    public string RutaArchivo { get; set; } = string.Empty;
    public long TamanoArchivo { get; set; }
    public string HashArchivo { get; set; } = string.Empty;
    public DateTime FechaCarga { get; set; } = DateTime.UtcNow;
    public int UsuarioCarga { get; set; }
    public bool Activo { get; set; } = true;

    // Navigation properties
    public Documento? Documento { get; set; }
    public ICollection<DocumentoProcesado> Procesamientos { get; set; } = new List<DocumentoProcesado>();
}
