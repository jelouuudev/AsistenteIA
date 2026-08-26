using Asistente.Domain.Entities;

namespace Asistente.Shared;

public class ActualizarAsistenteRequest
{
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public string? Objetivo { get; set; }
    public string? PromptSistema { get; set; }
    public string ModeloIA { get; set; } = "qwen2.5:14b";
    public EstadoAgente Estado { get; set; } = EstadoAgente.Borrador;
    public bool Activo { get; set; }
    public double? Temperatura { get; set; }
    public int? MaxTokens { get; set; }
    public int? TimeoutSegundos { get; set; }
    public string? Idioma { get; set; }
    public int? LongitudMaximaRespuesta { get; set; }
    public string? NivelFormalidad { get; set; }
    public string? FormatoRespuesta { get; set; }
    public string? Restricciones { get; set; }
    public string? MensajeBienvenida { get; set; }

    public List<int> Fuentes { get; set; } = new();
    public List<int> Herramientas { get; set; } = new();
    public List<int> Workflows { get; set; } = new();
    public List<int> Roles { get; set; } = new();
    public List<int> Usuarios { get; set; } = new();
}
