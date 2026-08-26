namespace Asistente.Shared;

public class CrearAsistenteRequest
{
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public string? Objetivo { get; set; }
    public string? PromptSistema { get; set; }
    public string ModeloIA { get; set; } = "qwen2.5:14b";
    public double? Temperatura { get; set; } = 0.7;
    public int? MaxTokens { get; set; } = 4096;
    public int? TimeoutSegundos { get; set; } = 300;
    public string? Idioma { get; set; } = "es";
    public int? LongitudMaximaRespuesta { get; set; }
    public string? NivelFormalidad { get; set; } = "profesional";
    public string? FormatoRespuesta { get; set; } = "texto";
    public string? Restricciones { get; set; }
    public string? MensajeBienvenida { get; set; }

    // Asignaciones iniciales (opcionales)
    public List<int> Fuentes { get; set; } = new();
    public List<int> Herramientas { get; set; } = new();
    public List<int> Workflows { get; set; } = new();
    public List<int> Roles { get; set; } = new();
    public List<int> Usuarios { get; set; } = new();
}
