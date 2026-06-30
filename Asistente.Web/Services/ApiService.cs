using System.Text;
using System.Text.Json;
using Asistente.Shared;

namespace Asistente.Web.Services;

public class ApiService : IApiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ApiService> _logger;

    public ApiService(HttpClient httpClient, ILogger<ApiService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<MensajeResponse> EnviarMensajeAsync(MensajeRequest request)
    {
        try
        {
            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("/api/chat/enviar", content);
            var responseContent = await response.Content.ReadAsStringAsync();

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var result = JsonSerializer.Deserialize<MensajeResponse>(responseContent, options);

            return result ?? new MensajeResponse
            {
                Exitoso = false,
                Error = "Error al procesar la respuesta del servidor."
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Error de conexión con la API.");
            return new MensajeResponse
            {
                Exitoso = false,
                Error = "No se puede conectar con el servicio de IA. Verifique que la API esté ejecutándose."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado al llamar a la API.");
            return new MensajeResponse
            {
                Exitoso = false,
                Error = "Ocurrió un error inesperado. Intente nuevamente."
            };
        }
    }
}
