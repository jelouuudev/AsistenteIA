namespace Asistente.Shared;

public class EmbeddingConfig
{
    public string Proveedor { get; set; } = "Ollama";
    public string ModeloEmbeddings { get; set; } = "nomic-embed-text";
    public string BaseVectorial { get; set; } = "ChromaDB";
    public int TopK { get; set; } = 5;
    public double PuntajeMinimo { get; set; } = 0.5;
    public int LongitudMaximaContexto { get; set; } = 4000;
}
