namespace Asistente.Shared;

public class ChromaConfig
{
    public string Url { get; set; } = "http://localhost:8000";
    public string CollectionName { get; set; } = "asistente_documentos";
    public int TimeoutSegundos { get; set; } = 30;
}
