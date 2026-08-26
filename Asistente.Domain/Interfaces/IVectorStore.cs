using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Asistente.Domain.Entities;

namespace Asistente.Domain.Interfaces;

public class VectorSearchFilter
{
    public List<int>? IdsFuentes { get; set; }
    public List<int>? IdsDocumentos { get; set; }
    public int? IdCategoria { get; set; }
    public string? TipoFuente { get; set; }
    public int? IdVersion { get; set; }
    public string? EstadoDocumento { get; set; }
    public bool IncluirHistoricos { get; set; } = false;
}

public interface IVectorStore
{
    Task IndexAsync(VectorDocument document);
    Task IndexBatchAsync(IEnumerable<VectorDocument> documents);
    Task<IEnumerable<VectorSearchResult>> SearchAsync(string query, int topK);
    Task<IEnumerable<VectorSearchResult>> SearchWithFilterAsync(string query, int topK, VectorSearchFilter filter);
    Task<IEnumerable<VectorSearchResult>> SearchByDocumentAsync(string query, int topK, string documentNameContains);
    Task<IEnumerable<string>> GetAllDocumentNamesAsync();
    Task<IEnumerable<VectorSearchResult>> GetByDocumentoProcesadoIdAsync(int documentoProcesadoId);
    Task<IEnumerable<int>> GetAllDocumentoProcesadoIdsAsync();
    Task DeleteDocumentAsync(Guid documentId);
    Task DeleteByDocumentoProcesadoIdAsync(int documentoProcesadoId);
    Task<int> GetDocumentCountAsync();
    Task<bool> HealthCheckAsync();
    Task<string> GetDocumentTextAsync(int documentoProcesadoId);
    Task ClearAsync();
    Task<IEnumerable<(string Nombre, int ChunkCount)>> GetDocumentCountsAsync();
}
