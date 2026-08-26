using System;

namespace Asistente.Shared;

public class DocumentoProcesadoDto
{
    public int IdDocumentoProcesado { get; set; }
    public int IdVersionDocumento { get; set; }
    public int IdDocumento { get; set; }
    public string DocumentoNombre { get; set; } = string.Empty;
    public string DocumentoCodigo { get; set; } = string.Empty;
    public int NumeroVersion { get; set; }
    public string NombreArchivo { get; set; } = string.Empty;
    public DateTime FechaInicio { get; set; }
    public DateTime? FechaFin { get; set; }
    public string Estado { get; set; } = string.Empty;
    public int TotalPaginas { get; set; }
    public int TotalCaracteres { get; set; }
    public int TotalChunks { get; set; }
    public string? Observaciones { get; set; }
}
