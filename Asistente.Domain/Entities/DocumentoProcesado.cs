using System;
using System.Collections.Generic;
using Asistente.Domain.Enums;

namespace Asistente.Domain.Entities;

public class DocumentoProcesado
{
    public int IdDocumentoProcesado { get; set; }
    public int IdVersionDocumento { get; set; }
    public DateTime FechaInicio { get; set; }
    public DateTime? FechaFin { get; set; }
    public EstadoProcesamiento Estado { get; set; } = EstadoProcesamiento.Pendiente;
    public int TotalPaginas { get; set; }
    public int TotalCaracteres { get; set; }
    public int TotalChunks { get; set; }
    public string? Observaciones { get; set; }

    // Navigation properties
    public DocumentoVersion? VersionDocumento { get; set; }
    public ICollection<DocumentoChunk> Chunks { get; set; } = new List<DocumentoChunk>();
}
