using System;
using System.Collections.Generic;

namespace Asistente.Shared;

public class ResultadoBusquedaVectorialDto
{
    public string Consulta { get; set; } = string.Empty;
    public int TotalResultados { get; set; }
    public List<FragmentoRelevanteDto> Fragmentos { get; set; } = new();
}

public class FragmentoRelevanteDto
{
    public Guid DocumentId { get; set; }
    public int ChunkId { get; set; }
    public int DocumentoProcesadoId { get; set; }
    public string Texto { get; set; } = string.Empty;
    public float PuntajeSimilitud { get; set; }
    public int Orden { get; set; }
    public int PaginaInicial { get; set; }
    public int PaginaFinal { get; set; }
    public string? DocumentoNombre { get; set; }
    public string? DocumentoCodigo { get; set; }
    public int SearchRank { get; set; }
    public int? IdDocumento { get; set; }
    public int? IdVersion { get; set; }
    public int? IdFuente { get; set; }
    public string? NombreFuente { get; set; }
    public int? IdCategoria { get; set; }
    public string? TipoDocumento { get; set; }
    public string? VersionDocumento { get; set; }
    public string? EstadoDocumento { get; set; }
}
