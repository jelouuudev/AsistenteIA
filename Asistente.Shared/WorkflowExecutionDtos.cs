namespace Asistente.Shared;

public class EjecutarWorkflowRequest
{
    public Dictionary<string, object>? Parametros { get; set; }
}

public class EjecutarWorkflowResponse
{
    public bool Exitoso { get; set; }
    public string Estado { get; set; } = string.Empty;
    public string? ResultadoFinal { get; set; }
    public long TiempoTotalMs { get; set; }
    public List<PasoEjecucionResponse> Pasos { get; set; } = new();
}

public class PasoEjecucionResponse
{
    public string Nombre { get; set; } = string.Empty;
    public string Herramienta { get; set; } = string.Empty;
    public bool Exitoso { get; set; }
    public string? Resultado { get; set; }
    public long TiempoMs { get; set; }
    public int Intentos { get; set; }
}
