namespace Asistente.Shared;

public class DocumentoChunkDto
{
    public int IdChunk { get; set; }
    public int IdDocumentoProcesado { get; set; }
    public int NumeroChunk { get; set; }
    public int PaginaInicial { get; set; }
    public int PaginaFinal { get; set; }
    public string Texto { get; set; } = string.Empty;
    public int TotalCaracteres { get; set; }
    public int Orden { get; set; }
}
