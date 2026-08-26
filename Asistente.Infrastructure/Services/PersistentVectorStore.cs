using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Asistente.Domain.Entities;
using Asistente.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace Asistente.Infrastructure.Services;

public class PersistentVectorStore : IVectorStore
{
    private readonly ConcurrentDictionary<string, VectorDocument> _documents = new();
    private readonly string _filePath;
    private readonly ILogger<PersistentVectorStore> _logger;
    private readonly IEmbeddingProvider _embeddingProvider;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public PersistentVectorStore(
        IEmbeddingProvider embeddingProvider,
        ILogger<PersistentVectorStore> logger)
    {
        _embeddingProvider = embeddingProvider;
        _logger = logger;
        
        var appDataPath = Path.Combine(AppContext.BaseDirectory, "vectorstore");
        Directory.CreateDirectory(appDataPath);
        _filePath = Path.Combine(appDataPath, "vectors.json");
        
        LoadFromDisk();
    }

    private void LoadFromDisk()
    {
        try
        {
            if (File.Exists(_filePath))
            {
                var json = File.ReadAllText(_filePath);
                var documents = JsonSerializer.Deserialize<List<VectorDocument>>(json, JsonOptions);
                
                if (documents != null)
                {
                    foreach (var doc in documents)
                    {
                        _documents[doc.DocumentId.ToString()] = doc;
                    }
                    _logger.LogInformation("Cargados {Count} documentos desde {_filePath}", _documents.Count, _filePath);
                }
            }
            else
            {
                _logger.LogInformation("No se encontró archivo de vectores en {_filePath}. Se creará uno nuevo.", _filePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al cargar vectores desde disco");
        }
    }

    private void SaveToDisk()
    {
        try
        {
            var documents = _documents.Values.ToList();
            var json = JsonSerializer.Serialize(documents, JsonOptions);
            File.WriteAllText(_filePath, json);
            _logger.LogInformation("Guardados {Count} documentos en {_filePath}", documents.Count, _filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al guardar vectores en disco");
        }
    }

    public async Task IndexAsync(VectorDocument document)
    {
        document.Text = document.Text ?? "";
        _documents.AddOrUpdate(
            document.DocumentId.ToString(),
            document,
            (_, _) => document);
        
        SaveToDisk();
    }

    public async Task IndexBatchAsync(IEnumerable<VectorDocument> documents)
    {
        foreach (var doc in documents)
        {
            doc.Text = doc.Text ?? "";
            _documents.AddOrUpdate(
                doc.DocumentId.ToString(),
                doc,
                (_, _) => doc);
        }
        
        SaveToDisk();
    }

    public async Task<IEnumerable<VectorSearchResult>> SearchAsync(string query, int topK)
    {
        if (!_documents.Any())
        {
            _logger.LogWarning("No hay documentos indexados.");
            return Enumerable.Empty<VectorSearchResult>();
        }

        float[] queryEmbedding;
        try
        {
            queryEmbedding = await _embeddingProvider.GenerateEmbeddingAsync(query);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generando embedding para la consulta");
            return Enumerable.Empty<VectorSearchResult>();
        }

        var scoredResults = _documents.Values
            .Where(doc => doc.Embedding != null && doc.Embedding.Length > 0)
            .Select(doc =>
            {
                var similarity = CosineSimilarity(queryEmbedding, doc.Embedding);
                return new { Doc = doc, Score = similarity };
            })
            .Where(r => r.Score > 0.01f)
            .OrderByDescending(r => r.Score)
            .Take(topK * 3)
            .ToList();

        var results = scoredResults
            .Select(r => new VectorSearchResult
            {
                DocumentId = r.Doc.DocumentId,
                ChunkId = r.Doc.ChunkId,
                DocumentoProcesadoId = r.Doc.DocumentoProcesadoId,
                Text = r.Doc.Text,
                Score = r.Score,
                Orden = r.Doc.Orden,
                PaginaInicial = r.Doc.PaginaInicial,
                PaginaFinal = r.Doc.PaginaFinal,
                MetadataDocumentoNombre = r.Doc.MetadataDocumentoNombre,
                MetadataDocumentoCodigo = r.Doc.MetadataDocumentoCodigo,
                IdDocumento = r.Doc.IdDocumento,
                IdVersion = r.Doc.IdVersion,
                IdFuente = r.Doc.IdFuente,
                IdCategoria = r.Doc.IdCategoria,
                TipoDocumento = r.Doc.TipoDocumento,
                VersionDocumento = r.Doc.VersionDocumento,
                EstadoDocumento = r.Doc.EstadoDocumento
            })
            .GroupBy(r => new { r.DocumentoProcesadoId, r.Orden })
            .Select(g => g.First())
            .OrderByDescending(r => r.Score)
            .ThenBy(r => r.Orden)
            .Take(topK)
            .ToList();

        _logger.LogInformation("Búsqueda '{Query}': {Count} resultados, mejor score: {Score:F4}",
            query.Length > 50 ? query[..50] + "..." : query,
            results.Count,
            results.Count > 0 ? results.Max(r => r.Score) : 0);

        return results;
    }

    public async Task<IEnumerable<VectorSearchResult>> SearchWithFilterAsync(string query, int topK, VectorSearchFilter filter)
    {
        var allResults = (await SearchAsync(query, Math.Max(topK * 3, 30))).ToList();

        if (filter.IdsFuentes != null && filter.IdsFuentes.Count > 0)
            allResults = allResults.Where(r => r.IdFuente.HasValue && filter.IdsFuentes.Contains(r.IdFuente.Value)).ToList();

        if (filter.IdsDocumentos != null && filter.IdsDocumentos.Count > 0)
            allResults = allResults.Where(r => r.IdDocumento.HasValue && filter.IdsDocumentos.Contains(r.IdDocumento.Value)).ToList();

        if (filter.IdCategoria.HasValue)
            allResults = allResults.Where(r => r.IdCategoria.HasValue && r.IdCategoria.Value == filter.IdCategoria.Value).ToList();

        if (filter.IdVersion.HasValue)
            allResults = allResults.Where(r => r.IdVersion.HasValue && r.IdVersion.Value == filter.IdVersion.Value).ToList();

        if (!string.IsNullOrWhiteSpace(filter.EstadoDocumento))
            allResults = allResults.Where(r => !string.IsNullOrWhiteSpace(r.EstadoDocumento) && r.EstadoDocumento == filter.EstadoDocumento).ToList();

        if (!filter.IncluirHistoricos)
            allResults = allResults.Where(r => string.IsNullOrWhiteSpace(r.EstadoDocumento) || r.EstadoDocumento != "Obsoleto").ToList();

        return allResults.OrderByDescending(r => r.Score).Take(topK);
    }

    public async Task<IEnumerable<VectorSearchResult>> SearchByDocumentAsync(string query, int topK, string documentNameContains)
    {
        if (!_documents.Any())
        {
            _logger.LogWarning("No hay documentos indexados.");
            return Enumerable.Empty<VectorSearchResult>();
        }

        var filtered = _documents.Values
            .Where(doc => !string.IsNullOrEmpty(doc.MetadataDocumentoNombre) &&
                          doc.MetadataDocumentoNombre.Contains(documentNameContains, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (filtered.Count == 0)
        {
            _logger.LogWarning("No se encontraron chunks del documento '{DocumentName}'.", documentNameContains);
            return Enumerable.Empty<VectorSearchResult>();
        }

        float[] queryEmbedding;
        try
        {
            queryEmbedding = await _embeddingProvider.GenerateEmbeddingAsync(query);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generando embedding para la consulta filtrada");
            return Enumerable.Empty<VectorSearchResult>();
        }

        var scoredResults = filtered
            .Where(doc => doc.Embedding != null && doc.Embedding.Length > 0)
            .Select(doc =>
            {
                var similarity = CosineSimilarity(queryEmbedding, doc.Embedding);
                return new { Doc = doc, Score = similarity };
            })
            .Where(r => r.Score > 0.01f)
            .OrderByDescending(r => r.Score)
            .Take(topK * 3)
            .ToList();

        var results = scoredResults
            .Select(r => new VectorSearchResult
            {
                DocumentId = r.Doc.DocumentId,
                ChunkId = r.Doc.ChunkId,
                DocumentoProcesadoId = r.Doc.DocumentoProcesadoId,
                Text = r.Doc.Text,
                Score = r.Score,
                Orden = r.Doc.Orden,
                PaginaInicial = r.Doc.PaginaInicial,
                PaginaFinal = r.Doc.PaginaFinal,
                MetadataDocumentoNombre = r.Doc.MetadataDocumentoNombre,
                MetadataDocumentoCodigo = r.Doc.MetadataDocumentoCodigo,
                IdDocumento = r.Doc.IdDocumento,
                IdVersion = r.Doc.IdVersion,
                IdFuente = r.Doc.IdFuente,
                IdCategoria = r.Doc.IdCategoria,
                TipoDocumento = r.Doc.TipoDocumento,
                VersionDocumento = r.Doc.VersionDocumento,
                EstadoDocumento = r.Doc.EstadoDocumento
            })
            .GroupBy(r => new { r.DocumentoProcesadoId, r.Orden })
            .Select(g => g.First())
            .OrderByDescending(r => r.Score)
            .ThenBy(r => r.Orden)
            .Take(topK)
            .ToList();

        _logger.LogInformation("Búsqueda filtrada por documento '{DocumentName}': {Count} resultados de {Total} chunks",
            documentNameContains, results.Count, filtered.Count);

        return results;
    }

    public Task DeleteDocumentAsync(Guid documentId)
    {
        _documents.TryRemove(documentId.ToString(), out _);
        SaveToDisk();
        return Task.CompletedTask;
    }

    public Task DeleteByDocumentoProcesadoIdAsync(int documentoProcesadoId)
    {
        var keysToRemove = _documents.Values
            .Where(d => d.DocumentoProcesadoId == documentoProcesadoId)
            .Select(d => d.DocumentId.ToString())
            .ToList();

        foreach (var key in keysToRemove)
            _documents.TryRemove(key, out _);

        SaveToDisk();
        return Task.CompletedTask;
    }

    public Task<int> GetDocumentCountAsync()
    {
        return Task.FromResult(_documents.Count);
    }

    public Task<IEnumerable<VectorSearchResult>> GetByDocumentoProcesadoIdAsync(int documentoProcesadoId)
    {
        var results = _documents.Values
            .Where(d => d.DocumentoProcesadoId == documentoProcesadoId)
            .GroupBy(d => d.Orden)
            .Select(g => g.First())
            .Select(doc => new VectorSearchResult
            {
                DocumentId = doc.DocumentId,
                ChunkId = doc.ChunkId,
                DocumentoProcesadoId = doc.DocumentoProcesadoId,
                Text = doc.Text,
                Score = 1.0f,
                Orden = doc.Orden,
                PaginaInicial = doc.PaginaInicial,
                PaginaFinal = doc.PaginaFinal,
                MetadataDocumentoNombre = doc.MetadataDocumentoNombre,
                MetadataDocumentoCodigo = doc.MetadataDocumentoCodigo,
                IdDocumento = doc.IdDocumento,
                IdVersion = doc.IdVersion,
                IdFuente = doc.IdFuente,
                IdCategoria = doc.IdCategoria,
                TipoDocumento = doc.TipoDocumento,
                VersionDocumento = doc.VersionDocumento,
                EstadoDocumento = doc.EstadoDocumento
            })
            .OrderBy(r => r.Orden)
            .ToList();

        return Task.FromResult((IEnumerable<VectorSearchResult>)results);
    }

    public Task<IEnumerable<int>> GetAllDocumentoProcesadoIdsAsync()
    {
        var ids = _documents.Values
            .Select(d => d.DocumentoProcesadoId)
            .Distinct()
            .AsEnumerable();
        return Task.FromResult(ids);
    }

    public Task<IEnumerable<string>> GetAllDocumentNamesAsync()
    {
        var names = _documents.Values
            .Select(d => d.MetadataDocumentoNombre ?? "")
            .Where(n => !string.IsNullOrEmpty(n))
            .Distinct()
            .AsEnumerable();
        return Task.FromResult(names);
    }

    public Task<IEnumerable<(string Nombre, int ChunkCount)>> GetDocumentCountsAsync()
    {
        var counts = _documents.Values
            .GroupBy(d => d.MetadataDocumentoNombre ?? "(sin nombre)")
            .Select(g => (Nombre: g.Key, ChunkCount: g.Count()))
            .AsEnumerable();
        return Task.FromResult(counts);
    }

    public Task ClearAsync()
    {
        _documents.Clear();
        SaveToDisk();
        _logger.LogInformation("Todos los documentos han sido eliminados del vector store.");
        return Task.CompletedTask;
    }

    public Task<bool> HealthCheckAsync()
    {
        return Task.FromResult(true);
    }

    public Task<string> GetDocumentTextAsync(int documentoProcesadoId)
    {
        var texts = _documents.Values
            .Where(d => d.DocumentoProcesadoId == documentoProcesadoId)
            .GroupBy(d => d.Orden)
            .Select(g => g.First())
            .OrderBy(d => d.Orden)
            .Select(d => d.Text);
        return Task.FromResult(string.Join("\n", texts));
    }

    private static float CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length || a.Length == 0)
            return 0f;

        float dot = 0f, normA = 0f, normB = 0f;
        for (int i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        if (normA == 0f || normB == 0f)
            return 0f;

        var similarity = dot / (MathF.Sqrt(normA) * MathF.Sqrt(normB));
        
        if (float.IsNaN(similarity) || float.IsInfinity(similarity))
            return 0f;
        
        similarity = Math.Clamp(similarity, -1f, 1f);
        
        return (similarity + 1f) / 2f;
    }
}
