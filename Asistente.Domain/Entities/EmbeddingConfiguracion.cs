namespace Asistente.Domain.Entities;

public class EmbeddingConfiguracion
{
    public int IdConfiguracion { get; set; }
    public string Proveedor { get; set; } = "Ollama";
    public string ModeloEmbeddings { get; set; } = "nomic-embed-text";
    public string BaseVectorial { get; set; } = "ChromaDB";
    public int CantidadResultados { get; set; } = 20;
    public double PuntajeMinimo { get; set; } = 0.15;
    public int LongitudMaximaContexto { get; set; } = 8000;
    public bool Activo { get; set; } = true;
}
