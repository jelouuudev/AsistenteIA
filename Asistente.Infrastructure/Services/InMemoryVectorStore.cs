using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Asistente.Domain.Entities;
using Asistente.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Asistente.Infrastructure.Services;

public class InMemoryVectorStore : IVectorStore
{
    private readonly ConcurrentDictionary<string, VectorDocument> _documents = new();
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<InMemoryVectorStore> _logger;

    private static readonly Dictionary<string, string> CorreccionesPdfPig = new(StringComparer.OrdinalIgnoreCase)
    {
        ["sugerente"] = "gerente",
        ["sugerentes"] = "gerentes",
    };

    private static string AplicarCorrecciones(string texto)
    {
        if (string.IsNullOrEmpty(texto)) return texto;
        foreach (var kvp in CorreccionesPdfPig)
            texto = Regex.Replace(texto, kvp.Key, kvp.Value, RegexOptions.IgnoreCase);
        texto = Regex.Replace(texto, @"\s*Pagina\s+\d+/\d+\s*", " ", RegexOptions.IgnoreCase);
        texto = Regex.Replace(texto, @"\s*P[áa]gina\s+\d+/\d+\s*", " ", RegexOptions.IgnoreCase);
        texto = Regex.Replace(texto, @"CloudSync Pro\s*[-–]\s*Manual del Producto\s*v\d+\.\d+[\d.]*", "", RegexOptions.IgnoreCase);
        texto = Regex.Replace(texto, @"(?m)^\s*TechCorp Solutions.*$", "", RegexOptions.IgnoreCase);
        texto = Regex.Replace(texto, @"(?m)^\s*Documento confidencial.*$", "", RegexOptions.IgnoreCase);
        return texto.Trim();
    }

    public InMemoryVectorStore(IServiceScopeFactory scopeFactory, ILogger<InMemoryVectorStore> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task IndexAsync(VectorDocument document)
    {
        document.Text = AplicarCorrecciones(document.Text ?? "");
        _documents.AddOrUpdate(
            document.DocumentId.ToString(),
            document,
            (_, _) => document);
    }

    public async Task IndexBatchAsync(IEnumerable<VectorDocument> documents)
    {
        foreach (var doc in documents)
        {
            doc.Text = AplicarCorrecciones(doc.Text ?? "");
            _documents.AddOrUpdate(
                doc.DocumentId.ToString(),
                doc,
                (_, _) => doc);
        }
    }

    public async Task<IEnumerable<VectorSearchResult>> SearchAsync(string query, int topK)
    {
        if (!_documents.Any())
        {
            _logger.LogWarning("No hay documentos indexados en memoria.");
            return Enumerable.Empty<VectorSearchResult>();
        }

        float[] queryEmbedding;
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var embeddingProvider = scope.ServiceProvider.GetRequiredService<IEmbeddingProvider>();
            queryEmbedding = await embeddingProvider.GenerateEmbeddingAsync(query);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generando embedding para la consulta, usando fallback a keywords.");
            return await SearchByKeywordsAsync(query, topK);
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

        _logger.LogInformation("Busqueda semantica '{Query}': {Count} resultados, mejor score: {Score:F4}",
            query.Length > 50 ? query[..50] + "..." : query,
            results.Count,
            results.Count > 0 ? results.Max(r => r.Score) : 0);

        return results;
    }

    private async Task<IEnumerable<VectorSearchResult>> SearchByKeywordsAsync(string query, int topK)
    {
        var queryLower = QuitarAcentos(query.ToLowerInvariant());
        var palabrasQuery = queryLower.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(p => p.Length > 2)
            .ToHashSet();

        if (palabrasQuery.Count == 0)
            return Enumerable.Empty<VectorSearchResult>();

        var scoredResults = _documents.Values
            .Select(doc =>
            {
                var textoLower = QuitarAcentos(doc.Text.ToLowerInvariant());
                var palabrasEncontradas = palabrasQuery.Count(p => textoLower.Contains(p));
                var score = palabrasQuery.Count > 0 ? (float)palabrasEncontradas / palabrasQuery.Count : 0f;

                if (palabrasEncontradas > 0)
                {
                    if (textoLower.Contains(queryLower))
                        score = Math.Max(score, 0.95f);
                }

                return new { Doc = doc, Score = Math.Min(score, 1.0f), palabrasEncontradas };
            })
            .Where(r => r.Score > 0.01f)
            .OrderByDescending(r => r.Score)
            .Take(topK)
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
            .ToList();

        _logger.LogInformation("Busqueda keywords (fallback) '{Query}': {Count} resultados, mejor score: {Score}",
            query.Length > 50 ? query[..50] + "..." : query,
            results.Count,
            results.Count > 0 ? results.Max(r => r.Score) : 0);

        return results;
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
            _logger.LogWarning("No hay documentos indexados en memoria.");
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
            using var scope = _scopeFactory.CreateScope();
            var embeddingProvider = scope.ServiceProvider.GetRequiredService<IEmbeddingProvider>();
            queryEmbedding = await embeddingProvider.GenerateEmbeddingAsync(query);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generando embedding para la consulta filtrada, usando fallback a keywords.");
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

        _logger.LogInformation("Busqueda filtrada por documento '{DocumentName}': {Count} resultados de {Total} chunks, mejor score: {Score:F4}",
            documentNameContains, results.Count, filtered.Count,
            results.Count > 0 ? results.Max(r => r.Score) : 0);

        return results;
    }

    public Task DeleteDocumentAsync(Guid documentId)
    {
        _documents.TryRemove(documentId.ToString(), out _);
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
        _logger.LogInformation("Todos los documentos han sido eliminados del store vectorial.");
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

    private static string QuitarAcentos(string texto)
    {
        if (string.IsNullOrEmpty(texto)) return texto;
        var normalizado = texto.Normalize(System.Text.NormalizationForm.FormD);
        var sb = new System.Text.StringBuilder();
        foreach (char c in normalizado)
        {
            var categoria = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
            if (categoria != System.Globalization.UnicodeCategory.ModifierLetter &&
                categoria != System.Globalization.UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }
        return sb.ToString().Normalize(System.Text.NormalizationForm.FormC);
    }
}