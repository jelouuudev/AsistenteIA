using System.Text.Json.Serialization;

namespace Asistente.Shared;

public class OllamaErrorResponse
{
    [JsonPropertyName("error")]
    public string Error { get; set; } = string.Empty;
}
