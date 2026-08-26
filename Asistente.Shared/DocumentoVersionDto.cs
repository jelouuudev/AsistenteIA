using System;

namespace Asistente.Shared;

public class DocumentoVersionDto
{
    public int IdVersion { get; set; }
    public int IdDocumento { get; set; }
    public int NumeroVersion { get; set; }
    public string NombreArchivo { get; set; } = string.Empty;
    public long TamanoArchivo { get; set; }
    public string HashArchivo { get; set; } = string.Empty;
    public DateTime FechaCarga { get; set; }
    public int UsuarioCarga { get; set; }
    public bool Activo { get; set; }

    public string TamanoFormateado => TamanoArchivo switch
    {
        < 1024 => $"{TamanoArchivo} B",
        < 1048576 => $"{TamanoArchivo / 1024.0:F1} KB",
        _ => $"{TamanoArchivo / 1048576.0:F1} MB"
    };
}
