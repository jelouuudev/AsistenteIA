using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Asistente.Domain.Interfaces;

/// <summary>
/// Solicitud de ejecución de una herramienta. El orquestador la construye a partir
/// de la decisión del modelo y la valida antes de invocar la herramienta.
/// </summary>
public class ToolExecutionRequest
{
    public string HerramientaCodigo { get; set; } = string.Empty;
    public Dictionary<string, object?> Parametros { get; set; } = new();
    public int IdUsuario { get; set; }
    public string? UsuarioNombre { get; set; }
    public int? IdAsistente { get; set; }
    public string? PreguntaOriginal { get; set; }
}

/// <summary>
/// Resultado de la ejecución de una herramienta.
/// </summary>
public class ToolExecutionResult
{
    public bool Exitoso { get; set; }
    public string? Contenido { get; set; }
    public string? Error { get; set; }
    public Dictionary<string, object?> Metadatos { get; set; } = new();
}

/// <summary>
/// Contrato base de toda herramienta del Motor de Herramientas.
/// Las herramientas están desacopladas del modelo de IA.
/// </summary>
public interface ITool
{
    string Name { get; }
    string Description { get; }
    string Categoria { get; }
    Task<ToolExecutionResult> ExecuteAsync(ToolExecutionRequest request, CancellationToken cancellationToken = default);
}
