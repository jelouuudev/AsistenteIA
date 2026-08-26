using System.Net.Http;
using System.Text;
using System.Text.Json;
using Asistente.Domain.Entities;
using Asistente.Domain.Interfaces;
using Asistente.Shared;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Asistente.Infrastructure.Services;

public class OllamaService : IOllamaService
{
    private readonly HttpClient _httpClient;
    private readonly OllamaConfig _config;
    private readonly ILogger<OllamaService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public OllamaService(HttpClient httpClient, IOptions<OllamaConfig> config, ILogger<OllamaService> logger)
    {
        _httpClient = httpClient;
        _config = config.Value;
        _logger = logger;
    }

    public async Task<string> SendMessageAsync(IEnumerable<Mensaje> historial, CancellationToken cancellationToken = default)
    {
        return await SendMessageAsync(historial, null, null, null, null, cancellationToken);
    }

    public async Task<string> SendMessageAsync(IEnumerable<Mensaje> historial, string? modelOverride = null, string? systemPrompt = null, double? temperature = null, int? maxTokens = null, CancellationToken cancellationToken = default)
    {
        var messages = new List<OllamaChatMessage>();

        if (!string.IsNullOrWhiteSpace(systemPrompt))
        {
            messages.Add(new OllamaChatMessage
            {
                Role = "system",
                Content = systemPrompt
            });
        }

        messages.AddRange(historial.Select(m => new OllamaChatMessage
        {
            Role = m.Rol.ToString().ToLowerInvariant(),
            Content = m.Contenido
        }));

        var model = modelOverride ?? _config.Modelo;

        var options = new OllamaRequestOptions();
        options.Temperature = temperature ?? _config.Temperatura;
        options.NumPredict = maxTokens ?? _config.MaxTokens;

        var request = new OllamaChatRequest
        {
            Model = model,
            Messages = messages,
            Stream = false,
            Options = options
        };

        var jsonContent = JsonSerializer.Serialize(request);
        var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

        _logger.LogInformation("Enviando mensaje a Ollama. Modelo: {Modelo}, URL: {Url}, Mensajes: {Cantidad}",
            model, _config.Url, messages.Count);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(_config.TimeoutSegundos));

        try
        {
            var response = await _httpClient.PostAsync("/api/chat", httpContent, cts.Token);
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync(cts.Token);

            var ollamaResponse = JsonSerializer.Deserialize<OllamaChatResponse>(responseContent, JsonOptions);

            if (ollamaResponse?.Message == null)
            {
                throw new InvalidOperationException("La respuesta de Ollama fue nula.");
            }

            return ollamaResponse.Message.Content;
        }
        catch (TaskCanceledException)
        {
            _logger.LogError("Timeout al comunicarse con Ollama después de {Timeout}s.", _config.TimeoutSegundos);
            throw new TimeoutException($"El servicio Ollama no respondió en {_config.TimeoutSegundos} segundos.");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Error de conexión con Ollama en {Url}", _config.Url);

            if (ex.StatusCode == null)
            {
                throw new InvalidOperationException(
                    "No se puede conectar con Ollama. Verifique que el servicio esté ejecutándose.");
            }

            if ((int)ex.StatusCode == 404)
            {
                throw new InvalidOperationException(
                    $"El modelo '{model}' no está disponible. Ejecute: ollama pull {model}");
            }

            if ((int)ex.StatusCode == 400)
            {
                _logger.LogError(ex, "Ollama rechazó la solicitud (400). El contexto podría ser demasiado grande. Modelo: {Modelo}", model);
                throw new InvalidOperationException(
                    "El contexto de la conversación es demasiado grande para el modelo. Intente con una conversación más corta.");
            }

            throw new InvalidOperationException($"Error en la comunicación con Ollama: {ex.Message}");
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Error al deserializar la respuesta de Ollama.");
            throw new InvalidOperationException("Error al procesar la respuesta del modelo de IA.");
        }
    }

    public async Task<bool> IsDisponibleAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(3));
            // Ping liviano al endpoint de Ollama; si no responde en 3s, se considera caído.
            var response = await _httpClient.GetAsync(_config.Url.TrimEnd('/') + "/api/tags", cts.Token);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Pre-flight Ollama no disponible en {Url}", _config.Url);
            return false;
        }
    }
}
