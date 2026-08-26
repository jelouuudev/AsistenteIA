namespace Asistente.Domain.Entities;

public class ConfiguracionRAG
{
    public int IdConfiguracion { get; set; }
    public int MaxChunks { get; set; } = 5;
    public int MaxCaracteresContexto { get; set; } = 12000;
    public double MinScore { get; set; } = 0.45;
    public int TopKPorFuente { get; set; } = 5;
    public int MaxFuentesConsultadas { get; set; } = 5;
    public int MaxReferencias { get; set; } = 10;
    public int MaxChunksAlModelo { get; set; } = 10;
    public bool UsarDocumentosHistoricos { get; set; } = false;
    public bool Activo { get; set; } = true;
}
