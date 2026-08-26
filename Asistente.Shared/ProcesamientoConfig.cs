namespace Asistente.Shared;

public class ProcesamientoConfig
{
    public int TamanoMaximoChunk { get; set; } = 1000;
    public int Solapamiento { get; set; } = 200;
    public int LongitudMinima { get; set; } = 100;
    public int FrecuenciaSegundos { get; set; } = 30;
    public int MaxDocumentosPorCiclo { get; set; } = 5;
}
