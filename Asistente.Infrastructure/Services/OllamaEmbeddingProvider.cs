using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Asistente.Domain.Interfaces;
using Asistente.Shared;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Asistente.Infrastructure.Services;

public class OllamaEmbeddingProvider : IEmbeddingProvider
{
    private readonly HttpClient _httpClient;
    private readonly EmbeddingConfig _config;
    private readonly ILogger<OllamaEmbeddingProvider> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public OllamaEmbeddingProvider(
        HttpClient httpClient,
        IOptions<EmbeddingConfig> config,
        ILogger<OllamaEmbeddingProvider> logger)
    {
        _httpClient = httpClient;
        _config = config.Value;
        _logger = logger;
    }

    public async Task<float[]> GenerateEmbeddingAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("El texto no puede estar vacío.");

        var request = new OllamaEmbeddingRequest
        {
            Model = _config.ModeloEmbeddings,
            Prompt = text
        };

        var jsonContent = JsonSerializer.Serialize(request);
        var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

        _logger.LogDebug("Generando embedding con modelo {Modelo} para texto de {Longitud} caracteres.",
            _config.ModeloEmbeddings, text.Length);

        try
        {
            var response = await _httpClient.PostAsync("/api/embeddings", httpContent);
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync();
            var ollamaResponse = JsonSerializer.Deserialize<OllamaEmbeddingResponse>(responseContent, JsonOptions);

            if (ollamaResponse?.Embedding == null || ollamaResponse.Embedding.Length == 0)
            {
                throw new InvalidOperationException("El proveedor Ollama devolvió un embedding vacío.");
            }

            _logger.LogDebug("Embed generado exitosamente. Dimensiones: {Dimensiones}", ollamaResponse.Embedding.Length);
            return Normalizar(ollamaResponse.Embedding);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Error de conexión con Ollama para embeddings.");

            if (ex.StatusCode == null)
            {
                throw new InvalidOperationException(
                    "No se puede conectar con Ollama para generar embeddings. Verifique que el servicio esté ejecutándose.");
            }

            if ((int)ex.StatusCode == 404)
            {
                throw new InvalidOperationException(
                    $"El modelo de embeddings '{_config.ModeloEmbeddings}' no está disponible. Ejecute: ollama pull {_config.ModeloEmbeddings}");
            }

            throw new InvalidOperationException($"Error en la comunicación con Ollama: {ex.Message}");
        }
    }

    /// <summary>
    /// Normaliza a norma unitaria: con vectores unitarios, la distancia L2 de Chroma
    /// equivale a similitud coseno (score = 1 - d²/2). Sin esto, L2 da cientos
    /// y ningún MinScore razonable filtra bien.
    /// </summary>
    private static float[] Normalizar(float[] v)
    {
        double norma = 0;
        foreach (var x in v) norma += (double)x * x;
        norma = Math.Sqrt(norma);
        if (norma < 1e-9) return v;
        var r = new float[v.Length];
        for (int i = 0; i < v.Length; i++) r[i] = (float)(v[i] / norma);
        return r;
    }
}

public class OllamaEmbeddingRequest
{
    public string Model { get; set; } = string.Empty;
    public string Prompt { get; set; } = string.Empty;
}

public class OllamaEmbeddingResponse
{
    public float[] Embedding { get; set; } = Array.Empty<float>();
}
