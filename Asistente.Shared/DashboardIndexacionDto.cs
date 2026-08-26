namespace Asistente.Shared;

public class DashboardIndexacionDto
{
    public int TotalDocumentosIndexados { get; set; }
    public int TotalChunksIndexados { get; set; }
    public int TotalEmbeddings { get; set; }
    public int PendientesIndexacion { get; set; }
    public int EnProcesoIndexacion { get; set; }
    public int ConErrorIndexacion { get; set; }
    public double TiempoPromedioIndexacionMs { get; set; }
    public bool EstadoBaseVectorial { get; set; }
}
