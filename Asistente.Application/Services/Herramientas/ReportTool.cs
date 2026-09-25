using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Asistente.Domain.Interfaces;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Asistente.Application.Services.Herramientas;

/// <summary>
/// Herramienta que genera un reporte estructurado en PDF a partir de datos previamente obtenidos.
/// Genera el archivo PDF en el sistema de archivos y devuelve la ruta + contenido markdown.
/// </summary>
public class ReportTool : ITool
{
    public string Name => "ReportTool";
    public string Description => "Genera un reporte estructurado en PDF a partir de datos previamente obtenidos.";
    public string Categoria => "Reporte";

    private readonly IFileStorageService _fileStorage;

    public ReportTool(IFileStorageService fileStorage)
    {
        _fileStorage = fileStorage;
    }

    public async Task<ToolExecutionResult> ExecuteAsync(ToolExecutionRequest request, CancellationToken cancellationToken = default)
    {
        var titulo = ObtenerString(request.Parametros, "titulo", "Reporte generado");
        var datos = ObtenerString(request.Parametros, "datos",
            request.Parametros.TryGetValue("contenido", out var c) ? (c?.ToString() ?? string.Empty) : string.Empty);

        if (string.IsNullOrWhiteSpace(datos))
            return new ToolExecutionResult { Exitoso = false, Error = "No se proporcionaron datos para el reporte." };

        // No generar un PDF "exitoso" cuando el paso previo no trajo información real.
        // DocumentSearchTool devuelve Exitoso=true con un texto marcador cuando RAG viene
        // vacío (el documento recién procesado aún no está indexado en Chroma), y SqlQueryTool
        // devuelve un marcador cuando la tabla no tiene filas. En ambos casos generar el PDF
        // solo produce un reporte basura ("Reporte: X / No se encontró...").
        if (EsMarcadorSinDatos(datos))
            return new ToolExecutionResult { Exitoso = false, Error = "Sin datos para el reporte: el paso previo no devolvió información (búsqueda documental sin resultados o consulta SQL sin filas). No se generó PDF." };

        try
        {
            // Generar contenido markdown
            var markdownContent = GenerarResumenEjecutivo(datos, titulo);

            // Generar PDF real con QuestPDF
            var pdfBytes = GenerarPdfDesdeMarkdown(markdownContent, titulo);

            // Guardar PDF en el sistema de archivos
            var sufijo = Guid.NewGuid().ToString("N")[..8];
            var nombreArchivo = $"reporte_{DateTime.UtcNow:yyyyMMdd_HHmmss}_{sufijo}.pdf";
            using var pdfStream = new MemoryStream(pdfBytes);
            var pdfPath = await _fileStorage.SaveFileAsync(pdfStream, nombreArchivo, "reportes");

            return new ToolExecutionResult
            {
                Exitoso = true,
                Contenido = markdownContent,
                Metadatos = new()
                {
                    ["titulo"] = titulo,
                    ["lineas"] = datos.Split('\n').Length,
                    ["pdfGenerado"] = true,
                    ["tamanoPdf"] = pdfBytes.Length,
                    ["rutaPdf"] = pdfPath,
                    ["nombreArchivo"] = nombreArchivo
                }
            };
        }
        catch (Exception ex)
        {
            return new ToolExecutionResult { Exitoso = false, Error = $"Error al generar PDF: {ex.Message}" };
        }
    }

    private byte[] GenerarPdfDesdeMarkdown(string markdown, string titulo)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(11).FontFamily("Arial"));

                page.Header()
                    .AlignRight()
                    .Text($"Generado: {DateTime.Now:dd/MM/yyyy HH:mm}")
                    .FontSize(8).FontColor(Colors.Grey.Medium);

                page.Content()
                    .Column(column =>
                    {
                        foreach (var linea in markdown.Split('\n'))
                        {
                            var trimmed = linea.Trim();

                            if (trimmed.StartsWith("# "))
                            {
                                column.Item().Text(trimmed[2..]).FontSize(20).Bold().FontColor(Colors.Blue.Darken2);
                            }
                            else if (trimmed.StartsWith("## "))
                            {
                                column.Item().PaddingTop(8).Text(trimmed[3..]).FontSize(16).Bold();
                            }
                            else if (trimmed.StartsWith("| "))
                            {
                                var celdas = trimmed.Split('|').Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()).ToList();
                                if (celdas.Count > 0 && !celdas.All(c => c.Replace("-", "").Trim().Length == 0))
                                {
                                    column.Item().PaddingTop(4).Row(row =>
                                    {
                                        foreach (var celda in celdas)
                                        {
                                            row.RelativeItem().Border(1).Padding(5).Text(celda).FontSize(10);
                                        }
                                    });
                                }
                            }
                            else if (trimmed.StartsWith("- "))
                            {
                                column.Item().PaddingTop(2).Row(row =>
                                {
                                    row.AutoItem().Text("• ");
                                    row.RelativeItem().Text(trimmed[2..]);
                                });
                            }
                            else if (trimmed.StartsWith("**") && trimmed.EndsWith("**"))
                            {
                                column.Item().PaddingTop(4).Text(trimmed.TrimStart('*').TrimEnd('*')).Bold();
                            }
                            else if (!string.IsNullOrEmpty(trimmed))
                            {
                                column.Item().PaddingTop(2).Text(trimmed);
                            }
                        }
                    });

                page.Footer()
                    .AlignCenter()
                    .Text(x =>
                    {
                        x.Span("Página ").FontSize(8);
                        x.CurrentPageNumber().FontSize(8);
                        x.Span(" de ").FontSize(8);
                        x.TotalPages().FontSize(8);
                    });
            });
        });

        using var stream = new MemoryStream();
        document.GeneratePdf(stream);
        return stream.ToArray();
    }

    internal static string GenerarResumenEjecutivo(string datos, string titulo)
    {
        // Contenido documental (prosa de RAG): NO es tabla. Renderizarlo como extracto
        // con sus fuentes. Antes caía al parser SQL y fabricaba "Total de registros" + celdas.
        if (EsContenidoDocumental(datos))
            return FormatearResumenDocumental(datos, titulo);

        var lineas = datos.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .Where(l => !l.StartsWith("Datos obtenidos") && !l.StartsWith("==") && l.Contains(':'))
            .ToList();

        if (lineas.Count == 0)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"# {titulo}");
            sb.AppendLine();
            sb.AppendLine($"_Generado: {DateTime.Now:dd/MM/yyyy HH:mm}_");
            sb.AppendLine();
            sb.AppendLine(datos);
            return sb.ToString();
        }

        var filas = new List<Dictionary<string, string>>();
        foreach (var linea in lineas)
        {
            var partes = linea.Split('|', StringSplitOptions.RemoveEmptyEntries);
            var fila = new Dictionary<string, string>();
            foreach (var parte in partes)
            {
                var kv = parte.Split(':', 2);
                if (kv.Length == 2)
                    fila[kv[0].Trim()] = kv[1].Trim();
            }
            if (fila.Count > 0) filas.Add(fila);
        }

        if (filas.Count == 0)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"# {titulo}");
            sb.AppendLine();
            sb.AppendLine($"_Generado: {DateTime.Now:dd/MM/yyyy HH:mm}_");
            sb.AppendLine();
            sb.AppendLine(datos);
            return sb.ToString();
        }

        var campoAgrupacion = filas[0].Keys.FirstOrDefault(k =>
            k.Equals("Estado", StringComparison.OrdinalIgnoreCase) ||
            k.Equals("Tipo", StringComparison.OrdinalIgnoreCase) ||
            k.Equals("Categoria", StringComparison.OrdinalIgnoreCase) ||
            k.Equals("Categoría", StringComparison.OrdinalIgnoreCase));

        var campoCantidad = filas[0].Keys.FirstOrDefault(k =>
            k.Equals("Cantidad", StringComparison.OrdinalIgnoreCase) ||
            k.Equals("Total", StringComparison.OrdinalIgnoreCase));

        var campoValor = filas[0].Keys.FirstOrDefault(k =>
            k.Equals("ValorTotal", StringComparison.OrdinalIgnoreCase) ||
            k.Equals("Precio", StringComparison.OrdinalIgnoreCase) ||
            k.Equals("Monto", StringComparison.OrdinalIgnoreCase) ||
            k.Equals("Valor", StringComparison.OrdinalIgnoreCase));

        var reporte = new System.Text.StringBuilder();
        reporte.AppendLine($"# {titulo}");
        reporte.AppendLine();
        reporte.AppendLine($"_Generado: {DateTime.Now:dd/MM/yyyy HH:mm}_");
        reporte.AppendLine();

        double totalGeneral = 0;
        double totalValor = 0;
        foreach (var fila in filas)
        {
            if (campoCantidad != null && fila.TryGetValue(campoCantidad, out var cStr) && double.TryParse(cStr.Replace(",", ""), out var cVal))
                totalGeneral += cVal;
            if (campoValor != null && fila.TryGetValue(campoValor, out var vStr) && double.TryParse(vStr.Replace(",", ""), out var vVal))
                totalValor += vVal;
        }

        if (totalGeneral == 0) totalGeneral = filas.Count;

        reporte.AppendLine($"**Total de registros:** {totalGeneral:F0}");
        if (totalValor > 0)
            reporte.AppendLine($"**Valor total:** {totalValor:N2}");
        reporte.AppendLine();

        // Tabla de detalle genérica: columnas reales de las filas (sirve para
        // cualquier tabla), una fila por registro. Antes emparejaba Estado->ID.
        var columnas = filas.SelectMany(f => f.Keys).Distinct().ToList();
        reporte.AppendLine("| " + string.Join(" | ", columnas) + " |");
        reporte.AppendLine("| " + string.Join(" | ", columnas.Select(_ => "---")) + " |");
        foreach (var fila in filas)
        {
            reporte.AppendLine("| " + string.Join(" | ", columnas.Select(c => fila.TryGetValue(c, out var v) ? v : "")) + " |");
        }

        if (campoAgrupacion != null)
        {
            var grupos = filas.GroupBy(f => campoAgrupacion != null && f.TryGetValue(campoAgrupacion, out var g) ? g : "");
            reporte.AppendLine();
            reporte.AppendLine("**Resumen:**");
            foreach (var grupo in grupos)
            {
                double cantidadGrupo = 0;
                var filasGrupo = grupo.ToList();
                foreach (var f in filasGrupo)
                {
                    if (campoCantidad != null && f.TryGetValue(campoCantidad, out var cStr) && double.TryParse(cStr.Replace(",", ""), out var cVal))
                        cantidadGrupo += cVal;
                    else
                        cantidadGrupo += 1;
                }
                var porcentaje = totalGeneral > 0 ? (cantidadGrupo * 100.0 / totalGeneral) : 0;
                reporte.AppendLine($"- {grupo.Key}: {cantidadGrupo:F0} ({porcentaje:F0}%)");
            }
        }

        return reporte.ToString();
    }

    private static string ObtenerString(Dictionary<string, object?> parametros, string clave, string fallback)
    {
        if (parametros.TryGetValue(clave, out var valor) && valor != null)
            return valor.ToString() ?? fallback;
        return fallback;
    }

    /// <summary>
    /// Detecta prosa documental de RAG (fragmentos con [Fuente:] o formato de contexto).
    /// No debe pasar por el parser tabular de SQL.
    /// </summary>
    private static bool EsContenidoDocumental(string datos)
        => datos.Contains("[Fuente:")
            || datos.Contains("CONTEXTO DOCUMENTAL")
            || datos.Contains("Según **");

    /// <summary>
    /// Formatea fragmentos documentales como extracto legible: título, fecha, conteo de
    /// fuentes y el contenido en prosa. Limpia marcadores internos de RAG (===, INSTRUCCIÓN).
    /// </summary>
    private static string FormatearResumenDocumental(string datos, string titulo)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"# {titulo}");
        sb.AppendLine();
        sb.AppendLine($"_Generado: {DateTime.Now:dd/MM/yyyy HH:mm}_");
        sb.AppendLine();

        var nFuentes = System.Text.RegularExpressions.Regex.Matches(datos, @"\[Fuente:").Count;
        if (nFuentes > 0)
            sb.AppendLine($"**Fuentes consultadas:** {nFuentes}");
        sb.AppendLine();

        foreach (var raw in datos.Split('\n'))
        {
            var linea = raw.TrimEnd();
            var t = linea.Trim();
            if (t.StartsWith("===") || t.StartsWith("INSTRUCCIÓN:")
                || t.StartsWith("- Copia") || t.StartsWith("- NO")
                || t.StartsWith("- Cita") || t.StartsWith("- Mantén")
                || t.StartsWith("Sección detectada:")) continue;
            if (t == "---") { sb.AppendLine(); continue; }
            sb.AppendLine(linea);
        }

        return sb.ToString();
    }

    /// <summary>
    /// Detecta los textos marcadores que las herramientas de consulta devuelven cuando no
    /// hay información real. Comparación insensible a mayúsculas y espacios para no dejar
    /// pasar variantes con saltos de línea o prefijos del workflow.
    /// </summary>
    private static bool EsMarcadorSinDatos(string datos)
    {        var normalizado = datos.Trim().ToLowerInvariant();
        return normalizado.Contains("no se encontró información documental relevante para la consulta")
            || normalizado.Contains("la consulta no devolvió registros")
            || normalizado.Contains("la consulta se ejecutó correctamente pero no devolvió registros");
    }
}
