namespace Asistente.Shared;

public class DashboardProcesamientoDto
{
    public int TotalDocumentos { get; set; }
    public int Pendientes { get; set; }
    public int EnProceso { get; set; }
    public int Procesados { get; set; }
    public int ConError { get; set; }
    public int TotalChunks { get; set; }
    public int TotalCaracteres { get; set; }
}
