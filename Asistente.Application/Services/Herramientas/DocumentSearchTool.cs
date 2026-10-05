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
    /// <summary>
    /// Marcador máquina-máquina cuando RAG no trae nada. Contrato interno:
    /// Orchestrator y ReportTool lo reconocen para no contaminar reportes.
    /// </summary>
    public const string SinResultadosMarcador = "No se encontró información documental relevante para la consulta.";

    private readonly IRagService _ragService;
    private readonly IRecuperacionService? _recuperacionService;
    private readonly ILogger<DocumentSearchTool> _logger;

    public DocumentSearchTool(
        IRagService ragService,
        ILogger<DocumentSearchTool> logger,
        IRecuperacionService? recuperacionService = null)
    {
        _ragService = ragService;
        _logger = logger;
        _recuperacionService = recuperacionService;
    }

    public string Name => "DocumentSearchTool";
    public string Description =>
        "Busca y recupera fragmentos de documentos internos mediante búsqueda semántica. " +
        "Úsala cuando el usuario pregunte por procedimientos, políticas, manuales o cualquier información documentada. " +
        "Parámetros: 'consulta' (texto a buscar), 'documento' (opcional: nombre del documento al que se deben " +
        "limitar los resultados, ej. al resumir un documento recién subido).";
    public string Categoria => "ConsultaDocumental";

    public async Task<ToolExecutionResult> ExecuteAsync(ToolExecutionRequest request, CancellationToken cancellationToken = default)
    {
        var consulta = ObtenerString(request.Parametros, "consulta", request.PreguntaOriginal ?? string.Empty);
        if (string.IsNullOrWhiteSpace(consulta))
            return new ToolExecutionResult { Exitoso = false, Error = "No se proporcionó una consulta de búsqueda." };

        // Preferencia de documento: fuerza a RAG a traer fragmentos DE ESE documento
        // (búsqueda dirigida con boost), en vez de depender solo de la similitud con la consulta.
        var preferencia = ObtenerString(request.Parametros, "documento", string.Empty);

        // 1) Alcance del asistente (respeta fuentes autorizadas, TopK/MinScore y fallback
        // lexico: el mismo camino que usa el chat y que sí encuentra fragmentos).
        if (request.IdAsistente.HasValue && _recuperacionService != null)
        {
            try
            {
                var (contextoAlcance, _) = await _recuperacionService.RecuperarContextoConFuentesAsync(
                    consulta, request.IdAsistente, request.IdUsuario, cancellationToken);
                if (!string.IsNullOrWhiteSpace(contextoAlcance))
                {
                    _logger.LogInformation("DocumentSearchTool con alcance del asistente {IdAsistente}: {N} caracteres.",
                        request.IdAsistente, contextoAlcance.Length);
                    return new ToolExecutionResult
                    {
                        Exitoso = true,
                        Contenido = contextoAlcance,
                        Metadatos = new() { ["alcance"] = "asistente" }
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Búsqueda con alcance de asistente falló; usando RAG global.");
            }
        }

        // 2) Fallback global (sin alcance por asistente).
        var contexto = await _ragService.RecuperarContextoDocumentalAsync(
            consulta, topK: 5,
            savedDocumentPreference: string.IsNullOrWhiteSpace(preferencia) ? null : preferencia.Trim());

        if (string.IsNullOrWhiteSpace(contexto.ContextoDocumental))
        {
            return new ToolExecutionResult
            {
                Exitoso = true,
                // Contrato compartido: el consumidor reconoce el token, no la prosa.
                // La frase legible se conserva para el usuario final.
                Contenido = ContratoResultado.MarcarSinDatos("sin fragmentos documentales relevantes")
                            + "\n" + SinResultadosMarcador,
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
