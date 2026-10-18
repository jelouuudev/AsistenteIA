using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Asistente.Domain.Entities;
using Asistente.Domain.Interfaces;

namespace Asistente.Infrastructure.Services;

/// <summary>
/// Envuelve un <see cref="IVectorStore"/> y le impide devolver vectores de memoria
/// institucional a quien no los pidió explícitamente.
///
/// CONTEXTO: documentos y memorias comparten la MISMA colección de vectores. Las
/// memorias se indexan con el marcador "MEMORIA:{codigo}" en el nombre del
/// documento. El RAG (RagService y RecuperacionService) autoriza por DOCUMENTO y
/// nunca ha aplicar el permiso "MEMORIA:{codigo}" que sí exige el Memory Manager,
/// así que un vector de memoria que se colara por una consulta de RAG llegaría al
/// prompt sin autorización. Y solo depende de la similitud: basta con que la
/// pregunta supere el umbral de score.
///
/// Decidirlo aquí y no en cada consumidor es lo que evita que el hueco vuelva: un
/// servicio nuevo que consulta vectores queda protegido por omisión. Quien sí
/// necesita leer memorias (SemanticMemorySearch, que recibe el store CRUDO por
/// inyección) es la excepción explícita.
///
/// Solo filtra lecturas. Las escrituras pasan intactas.
/// </summary>
public class MemorySafeVectorStore : IVectorStore
{
    /// <summary>Prefijo con el que las memorias se indexan en la colección compartida.</summary>
    public const string PrefijoMemoria = "MEMORIA:";

    private readonly IVectorStore _interno;

    public MemorySafeVectorStore(IVectorStore interno)
    {
        _interno = interno ?? throw new ArgumentNullException(nameof(interno));
    }

    public static bool EsVectorDeMemoria(VectorSearchResult? resultado)
        => resultado?.MetadataDocumentoNombre?.StartsWith(PrefijoMemoria, StringComparison.OrdinalIgnoreCase) == true;

    private static IEnumerable<VectorSearchResult> SinMemorias(IEnumerable<VectorSearchResult>? resultados)
        => resultados == null
            ? Enumerable.Empty<VectorSearchResult>()
            : resultados.Where(r => !EsVectorDeMemoria(r));

    public Task IndexAsync(VectorDocument document) => _interno.IndexAsync(document);

    public Task IndexBatchAsync(IEnumerable<VectorDocument> documents) => _interno.IndexBatchAsync(documents);

    public async Task<IEnumerable<VectorSearchResult>> SearchAsync(string query, int topK)
        => SinMemorias(await _interno.SearchAsync(query, topK));

    public async Task<IEnumerable<VectorSearchResult>> SearchWithFilterAsync(
        string query, int topK, VectorSearchFilter filter)
        => SinMemorias(await _interno.SearchWithFilterAsync(query, topK, filter));

    public async Task<IEnumerable<VectorSearchResult>> SearchByDocumentAsync(
        string query, int topK, string documentNameContains)
        => SinMemorias(await _interno.SearchByDocumentAsync(query, topK, documentNameContains));

    public async Task<IEnumerable<VectorSearchResult>> GetByDocumentoProcesadoIdAsync(int documentoProcesadoId)
        => SinMemorias(await _interno.GetByDocumentoProcesadoIdAsync(documentoProcesadoId));

    public async Task<IEnumerable<string>> GetAllDocumentNamesAsync()
        => (await _interno.GetAllDocumentNamesAsync())
            .Where(n => !n.StartsWith(PrefijoMemoria, StringComparison.OrdinalIgnoreCase));

    public Task<IEnumerable<int>> GetAllDocumentoProcesadoIdsAsync()
        => _interno.GetAllDocumentoProcesadoIdsAsync();

    public Task DeleteDocumentAsync(Guid documentId) => _interno.DeleteDocumentAsync(documentId);

    public Task DeleteByDocumentoProcesadoIdAsync(int documentoProcesadoId)
        => _interno.DeleteByDocumentoProcesadoIdAsync(documentoProcesadoId);

    public Task<int> GetDocumentCountAsync() => _interno.GetDocumentCountAsync();

    public Task<bool> HealthCheckAsync() => _interno.HealthCheckAsync();

    // El texto se pide por IdDocumentoProcesado real (las memorias no lo tienen:
    // se indexan con 0), así que no hay nada que discriminar aquí.
    public Task<string> GetDocumentTextAsync(int documentoProcesadoId)
        => _interno.GetDocumentTextAsync(documentoProcesadoId);

    public Task ClearAsync() => _interno.ClearAsync();

    public async Task<IEnumerable<(string Nombre, int ChunkCount)>> GetDocumentCountsAsync()
        => (await _interno.GetDocumentCountsAsync())
            .Where(c => !c.Nombre.StartsWith(PrefijoMemoria, StringComparison.OrdinalIgnoreCase));
}