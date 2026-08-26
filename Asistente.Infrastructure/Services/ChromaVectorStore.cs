using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Asistente.Domain.Entities;
using Asistente.Domain.Interfaces;
using Asistente.Shared;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Asistente.Infrastructure.Services;

public class ChromaVectorStore : IVectorStore
{
    private readonly HttpClient _httpClient;
    private readonly ChromaConfig _config;
    private readonly ILogger<ChromaVectorStore> _logger;
    private readonly IEmbeddingProvider _embeddingProvider;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private string? _collectionId;

    private static int GetMetadataInt(Dictionary<string, object> metadata, string key, int defaultValue)
    {
        if (!metadata.TryGetValue(key, out var val)) return defaultValue;
        return val switch
        {
            JsonElement je when je.ValueKind == JsonValueKind.Number => je.GetInt32(),
            JsonElement je when je.ValueKind == JsonValueKind.String && int.TryParse(je.GetString(), out var parsed) => parsed,
            int i => i,
            long l => (int)l,
            double d => (int)d,
            _ => defaultValue
        };
    }

    private static int? GetMetadataNullableInt(Dictionary<string, object> metadata, string key)
    {
        var val = GetMetadataInt(metadata, key, 0);
        return val == 0 ? null : val;
    }

    private static string? GetMetadataString(Dictionary<string, object> metadata, string key)
    {
        if (!metadata.TryGetValue(key, out var val)) return null;
        var str = val?.ToString();
        return string.IsNullOrWhiteSpace(str) ? null : str;
    }

    public ChromaVectorStore(
        HttpClient httpClient,
        IOptions<ChromaConfig> config,
        ILogger<ChromaVectorStore> logger,
        IEmbeddingProvider embeddingProvider)
    {
        _httpClient = httpClient;
        _config = config.Value;
        _logger = logger;
        _embeddingProvider = embeddingProvider;
    }

    public async Task IndexAsync(VectorDocument document)
    {
        await IndexBatchAsync(new[] { document });
    }

    public async Task IndexBatchAsync(IEnumerable<VectorDocument> documents)
    {
        var docs = documents.ToList();
        if (!docs.Any()) return;

        try
        {
            var collectionId = await ObtenerCollectionIdAsync();

            var ids = docs.Select(d => d.DocumentId.ToString()).ToList();
            var embeddings = docs.Select(d => d.Embedding.ToList()).ToList();
            var texts = docs.Select(d => d.Text).ToList();

            var metadatas = docs.Select(d => new Dictionary<string, object>
            {
                ["chunk_id"] = d.ChunkId,
                ["documento_procesado_id"] = d.DocumentoProcesadoId,
                ["orden"] = d.Orden,
                ["pagina_inicial"] = d.PaginaInicial,
                ["pagina_final"] = d.PaginaFinal,
                ["documento_nombre"] = d.MetadataDocumentoNombre ?? "",
                ["documento_codigo"] = d.MetadataDocumentoCodigo ?? "",
                ["id_documento"] = d.IdDocumento ?? 0,
                ["id_version"] = d.IdVersion ?? 0,
                ["id_fuente"] = d.IdFuente ?? 0,
                ["id_categoria"] = d.IdCategoria ?? 0,
                ["tipo_documento"] = d.TipoDocumento ?? "",
                ["version_documento"] = d.VersionDocumento ?? "",
                ["fecha_documento"] = d.FechaDocumento?.ToString("o") ?? "",
                ["estado_documento"] = d.EstadoDocumento ?? ""
            }).ToList();

            var request = new ChromaAddRequest
            {
                Ids = ids,
                Embeddings = embeddings,
                Documents = texts,
                Metadatas = metadatas
            };

            var json = JsonSerializer.Serialize(request, JsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var url = $"/api/v1/collections/{collectionId}/add";
            var response = await _httpClient.PostAsync(url, content);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("ChromaDB rechazó add con status {Status}: {Body}", response.StatusCode, responseBody);
                throw new Exception($"ChromaDB add falló ({response.StatusCode}): {responseBody}");
            }

            _logger.LogInformation("Indexados {Count} chunks en ChromaDB.", docs.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al indexar {Count} documentos en ChromaDB.", docs.Count);
            throw;
        }
    }

    public async Task<IEnumerable<VectorSearchResult>> SearchAsync(string query, int topK)
    {
        try
        {
            var collectionId = await ObtenerCollectionIdAsync();

            var queryEmbedding = await _embeddingProvider.GenerateEmbeddingAsync(query);

            var request = new
            {
                query_embeddings = new[] { queryEmbedding.ToList() },
                n_results = topK
            };

            var json = JsonSerializer.Serialize(request, JsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var url = $"/api/v1/collections/{collectionId}/query";
            var response = await _httpClient.PostAsync(url, content);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("ChromaDB rechazó query con status {Status}: {Body}", response.StatusCode, responseContent);
                return Enumerable.Empty<VectorSearchResult>();
            }

            if (string.IsNullOrWhiteSpace(responseContent) || !responseContent.TrimStart().StartsWith("{") && !responseContent.TrimStart().StartsWith("["))
            {
                _logger.LogWarning("ChromaDB devolvió respuesta no válida en SearchAsync: {Preview}",
                    responseContent.Length > 100 ? responseContent[..100] + "..." : responseContent);
                return Enumerable.Empty<VectorSearchResult>();
            }

            var chromaResponse = JsonSerializer.Deserialize<ChromaQueryResponse>(responseContent, JsonOptions);

            if (chromaResponse?.Ids == null || !chromaResponse.Ids.Any())
                return Enumerable.Empty<VectorSearchResult>();

            var resultados = new List<VectorSearchResult>();

            var ids = chromaResponse.Ids.FirstOrDefault() ?? new List<string>();
            var distances = chromaResponse.Distances?.FirstOrDefault() ?? new List<float>();
            var documents = chromaResponse.Documents?.FirstOrDefault() ?? new List<string>();
            var metadatas = chromaResponse.Metadatas?.FirstOrDefault() ?? new List<Dictionary<string, object>>();

            for (int i = 0; i < ids.Count; i++)
            {
                var score = distances.Count > i ? Convert.ToSingle(1.0 / (1.0 + distances[i])) : 0f;
                var metadata = metadatas.Count > i ? metadatas[i] : new Dictionary<string, object>();

                resultados.Add(new VectorSearchResult
                {
                    DocumentId = Guid.TryParse(ids[i], out var guid) ? guid : Guid.NewGuid(),
                    ChunkId = GetMetadataInt(metadata, "chunk_id", 0),
                    DocumentoProcesadoId = GetMetadataInt(metadata, "documento_procesado_id", 0),
                    Text = documents.Count > i ? documents[i] : string.Empty,
                    Score = score,
                    Orden = GetMetadataInt(metadata, "orden", i),
                    PaginaInicial = GetMetadataInt(metadata, "pagina_inicial", 0),
                    PaginaFinal = GetMetadataInt(metadata, "pagina_final", 0),
                    MetadataDocumentoNombre = metadata.TryGetValue("documento_nombre", out var dn) ? dn?.ToString() : null,
                    MetadataDocumentoCodigo = metadata.TryGetValue("documento_codigo", out var dc) ? dc?.ToString() : null,
                    IdDocumento = GetMetadataNullableInt(metadata, "id_documento"),
                    IdVersion = GetMetadataNullableInt(metadata, "id_version"),
                    IdFuente = GetMetadataNullableInt(metadata, "id_fuente"),
                    IdCategoria = GetMetadataNullableInt(metadata, "id_categoria"),
                    TipoDocumento = GetMetadataString(metadata, "tipo_documento"),
                    VersionDocumento = GetMetadataString(metadata, "version_documento"),
                    EstadoDocumento = GetMetadataString(metadata, "estado_documento")
                });
            }

            return resultados.OrderByDescending(r => r.Score);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al buscar en ChromaDB.");
            return Enumerable.Empty<VectorSearchResult>();
        }
    }

    public async Task<IEnumerable<VectorSearchResult>> SearchWithFilterAsync(string query, int topK, VectorSearchFilter filter)
    {
        try
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al buscar con filtros en ChromaDB.");
            return Enumerable.Empty<VectorSearchResult>();
        }
    }

    public async Task DeleteDocumentAsync(Guid documentId)
    {
        try
        {
            var collectionId = await ObtenerCollectionIdAsync();
            var url = $"/api/v1/collections/{collectionId}/delete";
            var request = new { ids = new[] { documentId.ToString() } };
            var json = JsonSerializer.Serialize(request, JsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(url, content);
            response.EnsureSuccessStatusCode();

            _logger.LogInformation("Documento {DocumentId} eliminado de ChromaDB.", documentId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al eliminar documento {DocumentId} de ChromaDB.", documentId);
            throw;
        }
    }

    public async Task DeleteByDocumentoProcesadoIdAsync(int documentoProcesadoId)
    {
        try
        {
            var collectionId = await ObtenerCollectionIdAsync();
            var url = $"/api/v1/collections/{collectionId}/delete";
            var request = new
            {
                where = new Dictionary<string, object>
                {
                    ["documento_procesado_id"] = documentoProcesadoId
                }
            };
            var json = JsonSerializer.Serialize(request, JsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(url, content);
            response.EnsureSuccessStatusCode();

            _logger.LogInformation("Vectores del documento procesado {Id} eliminados de ChromaDB.", documentoProcesadoId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al eliminar vectores del documento procesado {Id}.", documentoProcesadoId);
            throw;
        }
    }

    public async Task<int> GetDocumentCountAsync()
    {
        try
        {
            var collectionId = await ObtenerCollectionIdAsync();
            var url = $"/api/v1/collections/{collectionId}";
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();

            if (string.IsNullOrWhiteSpace(content) || !content.TrimStart().StartsWith("{"))
            {
                _logger.LogWarning("ChromaDB devolvió respuesta no válida en GetDocumentCountAsync.");
                return 0;
            }

            var collection = JsonSerializer.Deserialize<ChromaCollectionResponse>(content, JsonOptions);

            return collection?.Count ?? 0;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al obtener conteo de documentos de ChromaDB.");
            return 0;
        }
    }

    public async Task<IEnumerable<VectorSearchResult>> GetByDocumentoProcesadoIdAsync(int documentoProcesadoId)
    {
        try
        {
            var collectionId = await ObtenerCollectionIdAsync();

            var request = new
            {
                where = new Dictionary<string, object>
                {
                    ["documento_procesado_id"] = documentoProcesadoId
                }
            };

            var json = JsonSerializer.Serialize(request, JsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var url = $"/api/v1/collections/{collectionId}/get";
            var response = await _httpClient.PostAsync(url, content);

            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("ChromaDB rechazó get con status {Status}: {Body}", response.StatusCode, responseContent);
                return Enumerable.Empty<VectorSearchResult>();
            }

            if (string.IsNullOrWhiteSpace(responseContent) || !responseContent.TrimStart().StartsWith("{"))
            {
                _logger.LogWarning("ChromaDB devolvió respuesta no válida en GetByDocumentoProcesadoIdAsync.");
                return Enumerable.Empty<VectorSearchResult>();
            }

            var chromaResponse = JsonSerializer.Deserialize<ChromaGetResponse>(responseContent, JsonOptions);

            if (chromaResponse?.Ids == null || !chromaResponse.Ids.Any())
                return Enumerable.Empty<VectorSearchResult>();

            var ids = chromaResponse.Ids;
            var documents = chromaResponse.Documents ?? new List<string>();
            var metadatas = chromaResponse.Metadatas ?? new List<Dictionary<string, object>>();

            var resultados = new List<VectorSearchResult>();

            for (int i = 0; i < ids.Count; i++)
            {
                var metadata = metadatas.Count > i ? metadatas[i] : new Dictionary<string, object>();

                resultados.Add(new VectorSearchResult
                {
                    DocumentId = Guid.TryParse(ids[i], out var guid) ? guid : Guid.NewGuid(),
                    ChunkId = metadata.TryGetValue("chunk_id", out var cid) ? Convert.ToInt32(cid) : 0,
                    DocumentoProcesadoId = documentoProcesadoId,
                    Text = documents.Count > i ? documents[i] : string.Empty,
                    Score = 1.0f,
                    Orden = metadata.TryGetValue("orden", out var ord) ? Convert.ToInt32(ord) : i,
                    PaginaInicial = metadata.TryGetValue("pagina_inicial", out var pi) ? Convert.ToInt32(pi) : 0,
                    PaginaFinal = metadata.TryGetValue("pagina_final", out var pf) ? Convert.ToInt32(pf) : 0,
                    MetadataDocumentoNombre = metadata.TryGetValue("documento_nombre", out var dn) ? dn?.ToString() : null,
                    MetadataDocumentoCodigo = metadata.TryGetValue("documento_codigo", out var dc) ? dc?.ToString() : null
                });
            }

            return resultados.OrderBy(r => r.Orden);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener chunks del documento procesado {Id} en ChromaDB.", documentoProcesadoId);
            return Enumerable.Empty<VectorSearchResult>();
        }
    }

    public Task<IEnumerable<int>> GetAllDocumentoProcesadoIdsAsync()
    {
        _logger.LogWarning("GetAllDocumentoProcesadoIdsAsync no está soportado en ChromaVectorStore. Devolviendo vacío.");
        return Task.FromResult(Enumerable.Empty<int>());
    }

    public Task<IEnumerable<VectorSearchResult>> SearchByDocumentAsync(string query, int topK, string documentNameContains)
    {
        _logger.LogWarning("SearchByDocumentAsync no está soportado en ChromaVectorStore. Usando SearchAsync general.");
        return SearchAsync(query, topK);
    }

    public Task<IEnumerable<string>> GetAllDocumentNamesAsync()
    {
        _logger.LogWarning("GetAllDocumentNamesAsync no está soportado en ChromaVectorStore. Devolviendo vacío.");
        return Task.FromResult(Enumerable.Empty<string>());
    }

    public Task<IEnumerable<(string Nombre, int ChunkCount)>> GetDocumentCountsAsync()
    {
        _logger.LogWarning("GetDocumentCountsAsync no está soportado en ChromaVectorStore. Devolviendo vacío.");
        return Task.FromResult(Enumerable.Empty<(string, int)>());
    }

    public async Task<bool> HealthCheckAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("/api/v1/heartbeat");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task ClearAsync()
    {
        try
        {
            var url = $"/api/v1/collections/{_config.CollectionName}";
            var response = await _httpClient.DeleteAsync(url);
            if (response.IsSuccessStatusCode)
                _logger.LogInformation("Collection {Name} eliminada de ChromaDB.", _config.CollectionName);
            else
                _logger.LogWarning("No se pudo eliminar collection {Name} de ChromaDB (status {Status}).", _config.CollectionName, response.StatusCode);

            _collectionId = null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al limpiar ChromaDB.");
            throw;
        }
    }

    public async Task<string> GetDocumentTextAsync(int documentoProcesadoId)
    {
        try
        {
            var chunks = await GetByDocumentoProcesadoIdAsync(documentoProcesadoId);
            return string.Join("\n", chunks.OrderBy(c => c.Orden).Select(c => c.Text));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al obtener texto completo del documento procesado {Id}", documentoProcesadoId);
            return string.Empty;
        }
    }

    private async Task<string> ObtenerCollectionIdAsync()
    {
        if (_collectionId != null) return _collectionId;

        try
        {
            var url = $"/api/v1/collections/{_config.CollectionName}";
            var response = await _httpClient.GetAsync(url);
            var body = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var collection = JsonSerializer.Deserialize<ChromaCollectionResponse>(body, JsonOptions);
                if (collection?.Id != null)
                {
                    _collectionId = collection.Id;
                    return _collectionId;
                }
            }

            var createRequest = new { name = _config.CollectionName };
            var json = JsonSerializer.Serialize(createRequest, JsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var createResponse = await _httpClient.PostAsync("/api/v1/collections", content);
            var createBody = await createResponse.Content.ReadAsStringAsync();
            createResponse.EnsureSuccessStatusCode();

            var created = JsonSerializer.Deserialize<ChromaCollectionResponse>(createBody, JsonOptions);
            _collectionId = created?.Id ?? throw new Exception("No se pudo obtener el ID de la collection creada");
            _logger.LogInformation("Collection {Name} creada en ChromaDB con ID {Id}.", _config.CollectionName, _collectionId);
            return _collectionId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al verificar/crear collection en ChromaDB.");
            throw;
        }
    }
}

public class ChromaAddRequest
{
    public List<string> Ids { get; set; } = new();
    public List<List<float>> Embeddings { get; set; } = new();
    public List<string> Documents { get; set; } = new();
    public List<Dictionary<string, object>> Metadatas { get; set; } = new();
}

public class ChromaQueryRequest
{
    public string[] QueryTexts { get; set; } = Array.Empty<string>();
    public int NResults { get; set; } = 5;
}

public class ChromaQueryResponse
{
    public List<List<string>>? Ids { get; set; }
    public List<List<float>>? Distances { get; set; }
    public List<List<string>>? Documents { get; set; }
    public List<List<Dictionary<string, object>>>? Metadatas { get; set; }
}

public class ChromaCollectionResponse
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public int Count { get; set; }
}

public class ChromaGetResponse
{
    public List<string>? Ids { get; set; }
    public List<string>? Documents { get; set; }
    public List<Dictionary<string, object>>? Metadatas { get; set; }
}
