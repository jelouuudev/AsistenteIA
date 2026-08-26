using System.Text.Json.Serialization;

namespace Asistente.Shared;

public class OllamaChatRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("messages")]
    public List<OllamaChatMessage> Messages { get; set; } = [];

    [JsonPropertyName("stream")]
    public bool Stream { get; set; }

    [JsonPropertyName("options")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public OllamaRequestOptions? Options { get; set; }
}

public class OllamaRequestOptions
{
    [JsonPropertyName("temperature")]
    public double? Temperature { get; set; }

    [JsonPropertyName("num_predict")]
    public int? NumPredict { get; set; }
}
