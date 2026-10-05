namespace Asistente.Shared;

public class OllamaConfig
{
    public string Url { get; set; } = "http://localhost:11434";
    public string Modelo { get; set; } = "deepseek-r1-distill-qwen-7b";
    public int TimeoutSegundos { get; set; } = 120;
    public double Temperatura { get; set; } = 0.7;
    public int MaxTokens { get; set; } = 8192;
    /// <summary>Ventana de contexto por petición (Ollama default 4096; 8192 da
    /// margen a resultados de herramientas en planes multi-paso).</summary>
    public int NumCtx { get; set; } = 8192;
}
