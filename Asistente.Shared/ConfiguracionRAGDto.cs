namespace Asistente.Shared;

public class ConfiguracionRAGDto
{
    public int IdConfiguracion { get; set; }
    public int MaxChunks { get; set; }
    public int MaxCaracteresContexto { get; set; }
    public double MinScore { get; set; }
    public int TopKPorFuente { get; set; }
    public int MaxFuentesConsultadas { get; set; }
    public int MaxReferencias { get; set; }
    public int MaxChunksAlModelo { get; set; }
    public bool UsarDocumentosHistoricos { get; set; }
    public bool Activo { get; set; }
}

public class ActualizarConfiguracionRAGRequest
{
    public int MaxChunks { get; set; }
    public int MaxCaracteresContexto { get; set; }
    public double MinScore { get; set; }
    public int TopKPorFuente { get; set; }
    public int MaxFuentesConsultadas { get; set; }
    public int MaxReferencias { get; set; }
    public int MaxChunksAlModelo { get; set; }
    public bool UsarDocumentosHistoricos { get; set; }
}
