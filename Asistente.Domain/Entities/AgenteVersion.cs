using System.Text.Json;

namespace Asistente.Domain.Entities;

/// <summary>
/// Versión de configuración de un agente (ETAPA 16 - Actividad 8/17).
/// Conserva Prompt, Modelo, Configuración, Fuentes, Tools, Fecha y Usuario responsable.
/// Permite probar antes de publicar sin afectar la versión en producción.
/// </summary>
public class AgenteVersion
{
    public int IdAgenteVersion { get; set; }
    public int IdAsistente { get; set; }
    public int Version { get; set; }
    public string? PromptSistema { get; set; }
    public string? ModeloIA { get; set; }
    public double? Temperatura { get; set; }
    public int? MaxTokens { get; set; }

    /// <summary>
    /// Configuración completa serializada (fuentes, tools, workflows, restricciones)
    /// para reproducir el agente exactamente en esa versión.
    /// </summary>
    public string? Configuracion { get; set; }

    public EstadoAgente Estado { get; set; } = EstadoAgente.Borrador;
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    public string? UsuarioCreacion { get; set; }

    public Asistente? Asistente { get; set; }

    public string ConfiguracionJson
        => string.IsNullOrWhiteSpace(Configuracion)
            ? "{}"
            : Configuracion!;

    public void AplicarConfiguracion(ConfiguracionAgente cfg)
    {
        Configuracion = JsonSerializer.Serialize(cfg);
    }

    public ConfiguracionAgente ObtenerConfiguracion()
        => string.IsNullOrWhiteSpace(Configuracion)
            ? new ConfiguracionAgente()
            : JsonSerializer.Deserialize<ConfiguracionAgente>(Configuracion) ?? new ConfiguracionAgente();
}

/// <summary>
/// Snapshot inmutable de la configuración de un agente para una versión dada.
/// </summary>
public class ConfiguracionAgente
{
    public List<int> Fuentes { get; set; } = new();
    public List<int> Herramientas { get; set; } = new();
    public List<int> Workflows { get; set; } = new();
    public List<int> Roles { get; set; } = new();
    public List<int> Usuarios { get; set; } = new();
    public string? Restricciones { get; set; }
    public string? FormatoRespuesta { get; set; }
}
