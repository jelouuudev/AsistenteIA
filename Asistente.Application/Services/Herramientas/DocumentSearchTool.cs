using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Asistente.Application.Interfaces;
using Asistente.Domain.Interfaces;
using Asistente.Shared;
using Microsoft.Extensions.Logging;

namespace Asistente.Application.Services.Herramientas;

/// <summary>
/// Herramienta que recupera información desde el Motor RAG (búsqueda semántica documental).
/// Reutiliza IRagService; el modelo nunca accede directamente a los documentos.
/// </summary>
public class DocumentSearchTool : ITool
{
    private readonly IRagService _ragService;
    private readonly ILogger<DocumentSearchTool> _logger;

    public DocumentSearchTool(IRagService ragService, ILogger<DocumentSearchTool> logger)
    {
        _ragService = ragService;
        _logger = logger;
    }

    public string Name => "DocumentSearchTool";
    public string Description =>
        "Busca y recupera fragmentos de documentos internos mediante búsqueda semántica. " +
        "Úsala cuando el usuario pregunte por procedimientos, políticas, manuales o cualquier información documentada. " +
        "Parámetro: 'consulta' (texto a buscar).";
    public string Categoria => "ConsultaDocumental";

    public async Task<ToolExecutionResult> ExecuteAsync(ToolExecutionRequest request, CancellationToken cancellationToken = default)
    {
        var consulta = ObtenerString(request.Parametros, "consulta", request.PreguntaOriginal ?? string.Empty);
        if (string.IsNullOrWhiteSpace(consulta))
            return new ToolExecutionResult { Exitoso = false, Error = "No se proporcionó una consulta de búsqueda." };

        var contexto = await _ragService.RecuperarContextoDocumentalAsync(consulta, topK: 5);

        if (string.IsNullOrWhiteSpace(contexto.ContextoDocumental))
        {
            return new ToolExecutionResult
            {
                Exitoso = true,
                Contenido = "No se encontró información documental relevante para la consulta.",
                Metadatos = new() { ["totalFragmentos"] = 0 }
            };
        }

        _logger.LogInformation("DocumentSearchTool recuperó {N} fragmentos para: {Consulta}",
            contexto.TotalFragmentos, consulta);

        return new ToolExecutionResult
        {
            Exitoso = true,
            Contenido = contexto.ContextoDocumental,
            Metadatos = new()
            {
                ["totalFragmentos"] = contexto.TotalFragmentos,
                ["referencias"] = string.Join("; ", contexto.Referencias.Select(r => r.NombreDocumento))
            }
        };
    }

    private static string ObtenerString(Dictionary<string, object?> parametros, string clave, string fallback)
    {
        if (parametros.TryGetValue(clave, out var valor) && valor != null)
            return valor.ToString() ?? fallback;
        return fallback;
    }
}
