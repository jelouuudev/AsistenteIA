namespace Asistente.Shared;

public class OllamaConfig
{
    public string Url { get; set; } = "http://localhost:11434";
    public string Modelo { get; set; } = "deepseek-r1-distill-qwen-7b";
    public int TimeoutSegundos { get; set; } = 120;
}
