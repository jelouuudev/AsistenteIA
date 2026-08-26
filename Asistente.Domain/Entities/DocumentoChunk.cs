namespace Asistente.Domain.Entities;

public class DocumentoChunk
{
    public int IdChunk { get; set; }
    public int IdDocumentoProcesado { get; set; }
    public int NumeroChunk { get; set; }
    public int PaginaInicial { get; set; }
    public int PaginaFinal { get; set; }
    public string Texto { get; set; } = string.Empty;
    public int TotalCaracteres { get; set; }
    public int Orden { get; set; }

    // Navigation properties
    public DocumentoProcesado? DocumentoProcesado { get; set; }
}
