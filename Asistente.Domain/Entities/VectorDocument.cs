using System;

namespace Asistente.Domain.Entities;

public class VectorDocument
{
    public Guid DocumentId { get; set; }
    public int ChunkId { get; set; }
    public int DocumentoProcesadoId { get; set; }
    public string Text { get; set; } = string.Empty;
    public float[] Embedding { get; set; } = Array.Empty<float>();
    public int Orden { get; set; }
    public int PaginaInicial { get; set; }
    public int PaginaFinal { get; set; }
    public string? MetadataDocumentoNombre { get; set; }
    public string? MetadataDocumentoCodigo { get; set; }
    public int? IdDocumento { get; set; }
    public int? IdVersion { get; set; }
    public int? IdFuente { get; set; }
    public int? IdCategoria { get; set; }
    public string? TipoDocumento { get; set; }
    public string? VersionDocumento { get; set; }
    public DateTime? FechaDocumento { get; set; }
    public string? EstadoDocumento { get; set; }
}
