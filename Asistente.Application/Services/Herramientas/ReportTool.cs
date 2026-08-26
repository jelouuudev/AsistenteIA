using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Asistente.Domain.Interfaces;

namespace Asistente.Application.Services.Herramientas;

/// <summary>
/// Herramienta que genera un reporte estructurado en Markdown a partir de datos previamente
/// obtenidos de otras herramientas. Parámetro 'titulo' y 'datos' (texto o JSON).
/// </summary>
public class ReportTool : ITool
{
    public string Name => "ReportTool";
    public string Description =>
        "Genera un reporte estructurado en Markdown a partir de datos previamente obtenidos. " +
        "Parámetros: 'titulo' y 'datos' (texto o tabla). Úsala para presentar resultados de otras herramientas de forma ordenada.";
    public string Categoria => "Reporte";

    public Task<ToolExecutionResult> ExecuteAsync(ToolExecutionRequest request, CancellationToken cancellationToken = default)
    {
        var titulo = ObtenerString(request.Parametros, "titulo", "Reporte generado");
        var datos = ObtenerString(request.Parametros, "datos",
            request.Parametros.TryGetValue("contenido", out var c) ? (c?.ToString() ?? string.Empty) : string.Empty);

        if (string.IsNullOrWhiteSpace(datos))
            return Task.FromResult(new ToolExecutionResult { Exitoso = false, Error = "No se proporcionaron datos para el reporte." });

        var lineas = datos.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
        var reporte = new System.Text.StringBuilder();
        reporte.AppendLine($"# {titulo}");
        reporte.AppendLine();
        reporte.AppendLine($"_Generado: {DateTime.Now:dd/MM/yyyy HH:mm}_");
        reporte.AppendLine();

        if (lineas.Length > 1 && lineas[0].Contains(':'))
        {
            reporte.AppendLine("| Campo | Valor |");
            reporte.AppendLine("| --- | --- |");
            foreach (var linea in lineas.Take(30))
            {
                var partes = linea.Split(':', 2);
                reporte.AppendLine($"| {partes[0].Trim()} | {partes[1].Trim()} |");
            }
        }
        else
        {
            foreach (var linea in lineas.Take(30))
                reporte.AppendLine($"- {linea.Trim()}");
        }

        return Task.FromResult(new ToolExecutionResult
        {
            Exitoso = true,
            Contenido = reporte.ToString(),
            Metadatos = new() { ["titulo"] = titulo, ["lineas"] = lineas.Length }
        });
    }

    private static string ObtenerString(Dictionary<string, object?> parametros, string clave, string fallback)
    {
        if (parametros.TryGetValue(clave, out var valor) && valor != null)
            return valor.ToString() ?? fallback;
        return fallback;
    }
}
