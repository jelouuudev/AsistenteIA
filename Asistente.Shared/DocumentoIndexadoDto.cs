using System;

namespace Asistente.Shared;

public class DocumentoIndexadoDto
{
    public int IdDocumentoIndexado { get; set; }
    public int IdDocumentoProcesado { get; set; }
    public int IdDocumento { get; set; }
    public string DocumentoNombre { get; set; } = string.Empty;
    public string DocumentoCodigo { get; set; } = string.Empty;
    public DateTime FechaIndexacion { get; set; }
    public string Estado { get; set; } = string.Empty;
    public int TotalChunks { get; set; }
    public int TotalEmbeddings { get; set; }
    public string? Observaciones { get; set; }
}
