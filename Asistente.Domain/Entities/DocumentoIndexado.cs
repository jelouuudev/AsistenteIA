using System;
using Asistente.Domain.Enums;

namespace Asistente.Domain.Entities;

public class DocumentoIndexado
{
    public int IdDocumentoIndexado { get; set; }
    public int IdDocumentoProcesado { get; set; }
    public DateTime FechaIndexacion { get; set; }
    public EstadoIndexacion Estado { get; set; } = EstadoIndexacion.Pendiente;
    public int TotalChunks { get; set; }
    public int TotalEmbeddings { get; set; }
    public string? Observaciones { get; set; }

    // Navigation properties
    public DocumentoProcesado? DocumentoProcesado { get; set; }
}
