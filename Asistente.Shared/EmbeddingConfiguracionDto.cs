namespace Asistente.Shared;

public class EmbeddingConfiguracionDto
{
    public int IdConfiguracion { get; set; }
    public string Proveedor { get; set; } = string.Empty;
    public string ModeloEmbeddings { get; set; } = string.Empty;
    public string BaseVectorial { get; set; } = string.Empty;
    public int CantidadResultados { get; set; }
    public double PuntajeMinimo { get; set; }
    public int LongitudMaximaContexto { get; set; }
    public bool Activo { get; set; }
}

public class ActualizarEmbeddingConfiguracionRequest
{
    public string Proveedor { get; set; } = "Ollama";
    public string ModeloEmbeddings { get; set; } = "nomic-embed-text";
    public string BaseVectorial { get; set; } = "ChromaDB";
    public int CantidadResultados { get; set; } = 5;
    public double PuntajeMinimo { get; set; } = 0.5;
    public int LongitudMaximaContexto { get; set; } = 4000;
    public bool Activo { get; set; } = true;
}
