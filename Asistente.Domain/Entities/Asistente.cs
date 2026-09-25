namespace Asistente.Domain.Entities;

/// <summary>
/// Agente de Inteligencia Artificial especializado (evolución del Asistente - ETAPA 16).
/// Cada agente tiene propósito, instrucciones, modelo, fuentes, herramientas,
/// workflows y permisos propios, y puede versionarse y publicarse de forma independiente.
/// </summary>
public class Asistente
{
    public int IdAsistente { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public string? Objetivo { get; set; }

    // Prompt de sistema propio del agente
    public string? PromptSistema { get; set; }

    public string ModeloIA { get; set; } = "qwen2.5:14b";

    // Ciclo de vida (ETAPA 16)
    public EstadoAgente Estado { get; set; } = EstadoAgente.Activo;
    public int Version { get; set; } = 1;
    public bool Activo { get; set; } = true;
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    public DateTime? FechaModificacion { get; set; }

    // Configuration properties
    public string? Idioma { get; set; } = "es";
    public int? LongitudMaximaRespuesta { get; set; }
    public string? NivelFormalidad { get; set; } = "profesional";
    public string? FormatoRespuesta { get; set; } = "texto";
    public string? Restricciones { get; set; }
    public string? MensajeBienvenida { get; set; }
    public double? Temperatura { get; set; } = 0.3;
    public int? MaxTokens { get; set; } = 4096;
    public int? TimeoutSegundos { get; set; } = 300;

    // Navigation properties
    public ICollection<PromptSistema> PromptsSistema { get; set; } = new List<PromptSistema>();
    public ICollection<AsistenteFuente> AsistentesFuentes { get; set; } = new List<AsistenteFuente>();
    public ICollection<AsistenteHerramienta> AsistentesHerramientas { get; set; } = new List<AsistenteHerramienta>();
    public ICollection<AgenteWorkflow> AgentesWorkflows { get; set; } = new List<AgenteWorkflow>();
    public ICollection<AgenteRol> AgentesRoles { get; set; } = new List<AgenteRol>();
    public ICollection<AgenteVersion> Versiones { get; set; } = new List<AgenteVersion>();
    public ICollection<UsuarioAsistente> UsuariosAsistentes { get; set; } = new List<UsuarioAsistente>();
}
