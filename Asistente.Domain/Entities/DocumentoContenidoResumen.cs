namespace Asistente.Domain.Entities;

/// <summary>
/// Resumen del contenido ya indexado de un documento activo: código, nombre y el
/// texto concatenationado de sus fragmentos. Es la "firma" real del documento y
/// permite decidir por significado (embeddings) si una pregunta viene de él,
/// sin vocabulario fijo ni depender del nombre del archivo.
/// </summary>
public class DocumentoContenidoResumen
{
    public int IdDocumento { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public string Texto { get; set; } = string.Empty;
    public int TotalChunks { get; set; }
}