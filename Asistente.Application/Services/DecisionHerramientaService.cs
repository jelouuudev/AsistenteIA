using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Asistente.Domain.Entities;
using Asistente.Domain.Enums;
using Asistente.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace Asistente.Application.Services;

/// <summary>
/// Representa una decisión del modelo sobre qué herramienta usar.
/// </summary>
public class DecisionHerramienta
{
    public bool RequiereHerramienta { get; set; }
    public string? CodigoHerramienta { get; set; }
    public Dictionary<string, object?> Parametros { get; set; } = new();
}

/// <summary>
/// Motor de decisión (Actividad 5). Determina si la pregunta requiere una herramienta,
/// cuál usar y qué parámetros enviar, solicitando a DeepSeek un JSON de decisión (function-calling).
/// </summary>
public interface IDecisionHerramientaService
{
    Task<DecisionHerramienta> DecidirAsync(
        string pregunta,
        IEnumerable<Herramienta> herramientasDisponibles,
        CancellationToken cancellationToken = default);
}

public class DecisionHerramientaService : IDecisionHerramientaService
{
    private readonly IOllamaService _ollamaService;
    private readonly ILogger<DecisionHerramientaService> _logger;

    public DecisionHerramientaService(IOllamaService ollamaService, ILogger<DecisionHerramientaService> logger)
    {
        _ollamaService = ollamaService;
        _logger = logger;
    }

    public async Task<DecisionHerramienta> DecidirAsync(
        string pregunta,
        IEnumerable<Herramienta> herramientasDisponibles,
        CancellationToken cancellationToken = default)
    {
        var herramientas = herramientasDisponibles.ToList();
        if (herramientas.Count == 0)
            return new DecisionHerramienta { RequiereHerramienta = false };

        var catalogo = new StringBuilder();
        foreach (var h in herramientas)
        {
            catalogo.AppendLine($"- Codigo: {h.Codigo} | Categoria: {h.Categoria} | {h.Descripcion}");
        }

        var systemPrompt = $@"Eres el Motor de Decisión de un asistente empresarial. Tu única tarea es decidir si la pregunta del usuario requiere usar una de las herramientas disponibles.
Herramientas disponibles:
{catalogo}

Reglas estrictas:
1. Si la pregunta se puede responder con conocimiento general SIN necesidad de datos externos, responde con decisión 'requiereHerramienta: false'.
2. Si la pregunta requiere información de documentos, base de datos, cálculo matemático, fecha/hora o generación de reporte, responde 'requiereHerramienta: true' indicando el 'codigo' correcto y los 'parametros'.
3. Responde ÚNICAMENTE con un objeto JSON válido, sin texto adicional, sin etiquetas de código, sin razonamiento.
Formato:
{{""requiereHerramienta"": true, ""codigo"": ""SqlQueryTool"", ""parametros"": {{""pregunta"": ""total de clientes""}}}}
o
{{""requiereHerramienta"": false}}";

        var historial = new List<Mensaje>
        {
            new() { Rol = RolMensaje.User, Contenido = pregunta }
        };

        string respuesta;
        try
        {
            respuesta = await _ollamaService.SendMessageAsync(historial, null, systemPrompt, 0.1, 400, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en el motor de decisión de herramientas.");
            return new DecisionHerramienta { RequiereHerramienta = false };
        }

        return Parsear(respuesta, herramientas);
    }

    private static DecisionHerramienta Parsear(string respuesta, List<Herramienta> herramientas)
    {
        if (string.IsNullOrWhiteSpace(respuesta))
            return new DecisionHerramienta { RequiereHerramienta = false };

        // El modelo (DeepSeek) puede incluir bloques <think:6124c78e>...</think:6124c78e>; los removemos.
        var json = LimpiarJson(respuesta);
        if (string.IsNullOrWhiteSpace(json))
            return new DecisionHerramienta { RequiereHerramienta = false };

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var requiere = root.TryGetProperty("requiereHerramienta", out var r) && r.ValueKind == JsonValueKind.True;
            if (!requiere)
                return new DecisionHerramienta { RequiereHerramienta = false };

            var codigo = root.TryGetProperty("codigo", out var c) ? c.GetString() : null;
            var herramienta = herramientas.FirstOrDefault(h => h.Codigo == codigo);
            if (herramienta == null)
                return new DecisionHerramienta { RequiereHerramienta = false };

            var parametros = new Dictionary<string, object?>();
            if (root.TryGetProperty("parametros", out var p) && p.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in p.EnumerateObject())
                {
                    parametros[prop.Name] = prop.Value.ValueKind switch
                    {
                        JsonValueKind.String => prop.Value.GetString(),
                        JsonValueKind.Number => prop.Value.GetDouble(),
                        JsonValueKind.True => true,
                        JsonValueKind.False => false,
                        _ => prop.Value.GetRawText()
                    };
                }
            }

            return new DecisionHerramienta
            {
                RequiereHerramienta = true,
                CodigoHerramienta = herramienta.Codigo,
                Parametros = parametros
            };
        }
        catch (JsonException ex)
        {
            return new DecisionHerramienta { RequiereHerramienta = false };
        }
    }

    private static string LimpiarJson(string texto)
    {
        var sinThink = System.Text.RegularExpressions.Regex.Replace(texto, @"<\s*think\s*>[\s\S]*?<\s*/\s*think\s*>", "",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        var inicio = sinThink.IndexOf('{');
        var fin = sinThink.LastIndexOf('}');
        if (inicio >= 0 && fin > inicio)
            return sinThink.Substring(inicio, fin - inicio + 1);
        return string.Empty;
    }
}
