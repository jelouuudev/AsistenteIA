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

        // DEBUG: Log para ver qué datos llegan
        System.Diagnostics.Debug.WriteLine($"ReportTool recibió datos ({datos.Length} chars): {datos.Substring(0, Math.Min(500, datos.Length))}...");

        // La validación de datos ahora se hace en AgentOrchestrator (RecopilarDatosPreviosAsync).
        // ReportTool confía en que los datos que recibe ya fueron filtrados correctamente.
        // Solo validamos que no sea completamente vacío.

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
                page.DefaultTextStyle(x => x.FontSize(11).FontFamily("Liberation Sans"));

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
                                    // Tablas anchas (>8 columnas): las celdas Row se rompen
                                    // a mitad de palabra en A4. Se renderizan como bloque
                                    // de texto fluido legible en lugar de columnas.
                                    if (celdas.Count > 8)
                                    {
                                        column.Item().PaddingTop(4).Text(string.Join(" | ", celdas)).FontSize(9);
                                    }
                                    else
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
        // Contenido MIXTO (plan #7060: SQL + RAG en el mismo insumo): fusionar ambas
        // mitades en vez de elegir una. Criterio estructural: filas tabulares
        // ('|' + ':') + marcadores propios de RAG. Sin inspeccionar vocabulario.
        if (EsContenidoDocumental(datos) && TieneFilasTabulares(datos))
            return FormatearResumenMixto(datos, titulo);

        // Contenido documental (prosa de RAG): NO es tabla. Renderizarlo como extracto
        // con sus fuentes. Antes caía al parser SQL y fabricaba "Total de registros" + celdas.
        if (EsContenidoDocumental(datos))
            return FormatearResumenDocumental(datos, titulo);

        var lineas = datos.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .Where(l => !l.StartsWith("Datos obtenidos") && !l.StartsWith("==") && l.Contains(':'))
            .ToList();

        // Totales por SECCIÓN ([Paso N: ...]): con ramas paralelas cada sección trae
        // su propio header ("Total filtrado: 7."). Se suman los de pasos CONSULTA
        // (plantilla propia "Consultar datos"); si no hay, los de ANALISIS
        // ("Analizar"); si tampoco, el primero del texto (legado una rama).
        var (totalHeader, sumaHeader) = ExtraerTotalesPorSeccion(datos);

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

        // Calidad tabular: una fila real tiene ≥2 campos. La prosa con ':' sueltos
        // (ej: "la versión 1.7, la cabecera debería ser...") genera filas de 1 campo:
        // eso es documental, no tabla (antes fabricaba celdas basura).
        var filasValidas = filas.Where(f => f.Count >= 2).ToList();
        if (filasValidas.Count < 2)
            return FormatearResumenDocumental(datos, titulo);

        // Separar detalle vs agregado (estructural, sin nombres): la fila agregada
        // ({Total, ValorTotal} todo numérico, ≤3 campos) no debe mezclarse en la
        // tabla ni en los grupos (antes generaba grupo vacío '' y fila fantasma
        // con 8 | 11850.00, e inflaba la muestra de 6 a 7).
        var filasDetalle = filasValidas.Where(EsFilaDetalle).ToList();
        filas = filasDetalle.Count > 0 ? filasDetalle : filasValidas;
        filasValidas = filas;

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

        var campoAgrupacion = DetectarColumnaAgrupacion(filas)
            ?? ColumnaDeMarcadorPropio(datos, filas);

        // ¿Todo el insumo son filas agregadas (buckets) en vez de detalle?
        // En ese caso cada fila ya trae su conteo en una columna numérica.
        var soloAgregados = filasDetalle.Count == 0;
        string? colConteoBucket = null;
        if (soloAgregados)
        {
            colConteoBucket = filas[0].Keys.FirstOrDefault(k =>
                !k.Equals(campoAgrupacion, StringComparison.OrdinalIgnoreCase) && EsColumnaNumerica(filas, k));
        }

        var reporte = new System.Text.StringBuilder();
        reporte.AppendLine($"# {titulo}");
        reporte.AppendLine();
        reporte.AppendLine($"_Generado: {DateTime.Now:dd/MM/yyyy HH:mm}_");
        reporte.AppendLine();

        double totalGeneral = 0;
        double totalValor = 0;
        var sumaExacta = false;
        if (sumaHeader.HasValue)
        {
            totalValor = (double)sumaHeader.Value;
            sumaExacta = true;
        }
        foreach (var fila in filas)
        {
            // Detalle: cada fila es UN registro (nunca se suma una columna de
            // dominio como "Cantidad"=unidades). Buckets agregados: su columna.
            if (soloAgregados && colConteoBucket != null && fila.TryGetValue(colConteoBucket, out var bStr) && double.TryParse(bStr.Replace(",", ""), out var bVal))
                totalGeneral += bVal;
        }

        if (totalGeneral == 0) totalGeneral = filas.Count;
        // Si el header trae el total exacto y las filas son una muestra, manda el header.
        var esMuestra = totalHeader is > 0 && filas.Count < totalHeader;
        if (totalHeader is > 0) totalGeneral = totalHeader.Value;

        reporte.AppendLine(esMuestra
            ? $"**Total de registros:** {totalGeneral:F0} (detalle: muestra de {filas.Count})"
            : $"**Total de registros:** {totalGeneral:F0}");
        if (totalValor > 0)
            reporte.AppendLine(sumaExacta || !esMuestra
                ? $"**Valor total:** {totalValor:N2}"
                : $"**Valor total (muestra):** {totalValor:N2}");
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
            reporte.AppendLine(esMuestra ? "**Resumen (muestra):**" : "**Resumen:**");
            double contadoEnGrupos = 0;
            foreach (var grupo in grupos)
            {
                if (string.IsNullOrWhiteSpace(grupo.Key)) continue;
                double cantidadGrupo = 0;
                var filasGrupo = grupo.ToList();
                foreach (var f in filasGrupo)
                {
                    // Detalle: +1 por registro (una columna "Cantidad" de dominio
                    // son unidades, no registros: sumarla daba 357%). Buckets: su columna.
                    if (soloAgregados && colConteoBucket != null && f.TryGetValue(colConteoBucket, out var cStr) && double.TryParse(cStr.Replace(",", ""), out var cVal))
                        cantidadGrupo += cVal;
                    else
                        cantidadGrupo += 1;
                }
                contadoEnGrupos += cantidadGrupo;
                var porcentaje = totalGeneral > 0 ? (cantidadGrupo * 100.0 / totalGeneral) : 0;
                reporte.AppendLine($"- {grupo.Key}: {cantidadGrupo:F0} ({porcentaje:F0}%)");
            }
            var resto = totalGeneral - contadoEnGrupos;
            if (esMuestra && resto > 0)
                reporte.AppendLine($"- Otros (fuera de muestra): {resto:F0}");
        }

        return reporte.ToString();
    }

    /// <summary>
    /// ¿Fila de detalle (vs fila agregada)? Agregada = todos sus valores numéricos
    /// y ≤3 campos (ej: {Total: 8, ValorTotal: 11850}). Estructural, sin nombres.
    /// </summary>
    private static bool EsFilaDetalle(Dictionary<string, string> fila)
        => !(fila.Count <= 3 && fila.Values.All(v => string.IsNullOrWhiteSpace(v) || EsNumerico(v)));

    /// <summary>
    /// Columna de agrupación por ESTRUCTURA (sin nombres fijos): primera columna
    /// de texto con 2..N valores distintos (N = mín(10, filas-1)). Ej.: Categoria
    /// con 2 valores en 6 filas; Estado con 2 en 2 filas agregadas. Las listas de
    /// nombres quedan solo como último recurso.
    /// </summary>
    /// <summary>
    /// Columna de agrupación desde los MARCADORES PROPIOS del contenido
    /// ("Desglose por {col}" o "Filtro aplicado: WHERE [{col}]"), que pone el
    /// propio SqlQueryTool al final de su salida. Es contrato máquina-máquina:
    /// los nombres salen de los datos consultados, no de una lista. Sustituye al
    /// antiguo respaldo con nombres fijos ("Estado", "Tipo", "Categoria").
    /// </summary>
    private static string? ColumnaDeMarcadorPropio(string datos, List<Dictionary<string, string>> filas)
    {
        try
        {
            var claves = new HashSet<string>(filas.SelectMany(f => f.Keys), StringComparer.OrdinalIgnoreCase);
            var m = System.Text.RegularExpressions.Regex.Match(datos, @"Desglose por (\w+)");
            if (m.Success && claves.Contains(m.Groups[1].Value)) return m.Groups[1].Value;
            var w = System.Text.RegularExpressions.Regex.Match(datos, @"WHERE\s*\[([^\]]+)\]");
            if (w.Success && claves.Contains(w.Groups[1].Value)) return w.Groups[1].Value;
            return null;
        }
        catch { return null; }
    }

    private static string? DetectarColumnaAgrupacion(List<Dictionary<string, string>> filas)
    {
        try
        {
            if (filas.Count == 0) return null;
            var maxD = Math.Min(10, Math.Max(2, filas.Count - 1));
            return filas[0].Keys
                .Where(k => filas.All(f => !f.TryGetValue(k, out var v) || string.IsNullOrWhiteSpace(v) || !EsNumerico(v)))
                .Select(k => new
                {
                    K = k,
                    D = filas.Select(f => f.TryGetValue(k, out var v) ? (v ?? "") : "")
                        .Distinct(StringComparer.OrdinalIgnoreCase).Count()
                })
                .Where(x => x.D >= 2 && x.D <= maxD)
                .OrderBy(x => x.D)
                .Select(x => x.K)
                .FirstOrDefault();
        }
        catch { return null; }
    }

    private static bool EsNumerico(string valor)
    {
        var limpio = valor.Replace(",", "").Replace(" ", "").TrimEnd('%').Trim();
        return double.TryParse(limpio,
            System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out _);
    }

    /// <summary>
    /// Extrae la suma exacta del header propio de SqlQueryTool
    /// ("Valor total: 11180.00."). Contrato máquina-máquina.
    /// </summary>
    private static decimal? ExtraerSumaHeader(string datos)
    {
        // Formato invariante F2 del header ("Valor total: 11850.00."): el punto
        // final de frase no forma parte del número. También las etiquetas propias
        // del agregado ("Grupo: Lima | Total: 6 | Unidades: 19 | ValorTotal:
        // 17300.00", "importe total 17300.00", "suma de la columna decimal N"):
        // son la suma YA calculada en SQL por la propia herramienta (contrato
        // máquina-máquina, "no recalcular"). Sustituyen a la antigua lista de
        // nombres ("ValorTotal/Precio/Monto/Valor"), que adivinaba qué columna
        // sumar por su nombre y fallaba con cualquier otro esquema.
        var m = System.Text.RegularExpressions.Regex.Match(
            datos, @"(?:Valor\s*total|importe\s*total|suma de la columna decimal)\s*:?\s*([\d,]+(?:\.\d+)?)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (m.Success && decimal.TryParse(m.Groups[1].Value.Replace(",", ""),
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var suma) && suma != 0)
            return suma;
        return null;
    }

    /// <summary>
    /// Totales sumados por SECCIÓN ([Paso N: ...]) para ramas paralelas: cada rama
    /// trae su header ("Total filtrado: 7."). Suman las secciones CONSULTA
    /// (plantilla propia "Consultar datos"); si no hay, las ANALISIS ("Analizar");
    /// si tampoco, el primer header del texto (legado una rama). Contratos
    /// máquina-máquina: nombres de plantilla + formato de header propios.
    /// </summary>
    private static (int? Total, decimal? Suma) ExtraerTotalesPorSeccion(string datos)
    {
        try
        {
            var partes = System.Text.RegularExpressions.Regex.Split(datos, @"(?=^\[Paso \d+:)",
                System.Text.RegularExpressions.RegexOptions.Multiline | System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (partes.Length > 1)
            {
                var consulta = new List<string>();
                var analisis = new List<string>();
                foreach (var p in partes)
                {
                    var primera = p.Split('\n').FirstOrDefault() ?? string.Empty;
                    if (primera.Contains("Consultar datos", StringComparison.OrdinalIgnoreCase)) consulta.Add(p);
                    else if (primera.Contains("Analizar", StringComparison.OrdinalIgnoreCase)) analisis.Add(p);
                }
                var elegidas = consulta.Count > 0 ? consulta : analisis;
                if (elegidas.Count > 0)
                {
                    int total = 0;
                    decimal suma = 0;
                    var hayTotal = false;
                    var haySuma = false;
                    foreach (var p in elegidas)
                    {
                        var t = ExtraerTotalHeader(p);
                        if (t.HasValue) { total += t.Value; hayTotal = true; }
                        var s = ExtraerSumaHeader(p);
                        if (s.HasValue) { suma += s.Value; haySuma = true; }
                    }
                    if (hayTotal) return (total, haySuma ? suma : null);
                }
            }
        }
        catch { }
        return (ExtraerTotalHeader(datos), ExtraerSumaHeader(datos));
    }

    private static bool TieneSeccionesDePasos(string datos)
    {
        try
        {
            return System.Text.RegularExpressions.Regex.Matches(datos, @"^\[Paso \d+:",
                System.Text.RegularExpressions.RegexOptions.Multiline | System.Text.RegularExpressions.RegexOptions.IgnoreCase).Count > 1;
        }
        catch { return false; }
    }

    /// <summary>
    /// Totales POR TABLA, leídos de las líneas autocontenidas que emite
    /// SqlQueryTool ("Datos obtenidos de la tabla 'insumos' (base de datos
    /// 'ComidaTest'): Total: 8 | Unidades: 343 | ValorTotal: 2723.50"). Cada línea
    /// se auto-declara, así que el desglose no depende de los marcadores "[Paso N:]"
    /// ni de que el contexto haya sido recortado. Sin vocabulario de dominio: el
    /// nombre de la tabla y de la base los pone la propia herramienta.
    /// </summary>
    private static List<(string Etiqueta, int? Total, decimal? Suma)> ExtraerTotalesPorTabla(string datos)
    {
        var porEtiqueta = new Dictionary<string, (int? Total, decimal? Suma)>(StringComparer.OrdinalIgnoreCase);
        var orden = new List<string>();
        foreach (var linea in datos.Split('\n'))
        {
            var m = System.Text.RegularExpressions.Regex.Match(linea,
                @"de la tabla\s*'([^']+)'(?:\s*\(\s*base de datos\s*'([^']*)'\s*\))?",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (!m.Success) continue;
            var tabla = m.Groups[1].Value.Trim();
            var bd = m.Groups[2].Success ? m.Groups[2].Value.Trim() : string.Empty;
            var etiqueta = string.IsNullOrEmpty(bd) ? tabla : $"{bd}.{tabla}";
            var total = ExtraerTotalHeader(linea);
            var suma = ExtraerSumaHeader(linea);
            if (!total.HasValue && !suma.HasValue) continue;
            if (porEtiqueta.TryGetValue(etiqueta, out var previa))
            {
                // La consulta y su análisis reiterate la misma tabla: se completa el
                // dato que falte en vez de duplicar la fila.
                porEtiqueta[etiqueta] = (previa.Total ?? total, previa.Suma ?? suma);
            }
            else
            {
                porEtiqueta[etiqueta] = (total, suma);
                orden.Add(etiqueta);
            }
        }
        return orden.Select(e => (e, porEtiqueta[e].Total, porEtiqueta[e].Suma)).ToList();
    }

    /// <summary>
    /// Escribe los totales SQL del informe. Con UNA sola tabla se mantiene el gran
    /// total (suma de las ramas paralelas de esa tabla). Con VARIAS tablas NO se
    /// inventa un total agregado: sumar "ventas" y "activos" de bases distintas no
    /// significa nada, y presentar solo la última capa (plan #12197: 24 registros /
    /// 33.050 de 'ventas' como si fueran el total) era engañoso.
    /// </summary>
    private static bool EscribirTotales(System.Text.StringBuilder sb, string datos)
    {
        var porTabla = ExtraerTotalesPorTabla(datos);
        if (porTabla.Count > 1)
        {
            sb.AppendLine("## Totales por tabla");
            sb.AppendLine();
            foreach (var (etiqueta, total, suma) in porTabla)
            {
                var partes = new List<string>();
                if (total is > 0) partes.Add($"{total.Value:F0} registros");
                if (suma.HasValue) partes.Add($"valor {suma.Value:N2}");
                sb.AppendLine($"- **{etiqueta}**: {(partes.Count > 0 ? string.Join(" · ", partes) : "sin totales")}");
            }
            sb.AppendLine();
            return true;
        }
        var (totalHeader, sumaHeader) = ExtraerTotalesPorSeccion(datos);
        var escrito = false;
        if (totalHeader is > 0)
        {
            sb.AppendLine($"**Total de registros (SQL):** {totalHeader.Value:F0}");
            escrito = true;
        }
        if (sumaHeader.HasValue)
        {
            sb.AppendLine($"**Valor total (SQL):** {sumaHeader.Value:N2}");
            escrito = true;
        }
        if (escrito)
            sb.AppendLine();
        return escrito;
    }

    /// <summary>
    /// ¿Cuántas marcas de total/suma hay en el texto? Las marcas son las etiquetas
    /// propias de SqlQueryTool ("Total:", "Total filtrado:", "Valor total:",
    /// "importe total", "suma de la columna decimal"). Si hay más de una, el
    /// texto trae varios grupos y no se puede tomar la primera como gran total.
    /// </summary>
    private static int ContarMarcasTotal(string datos)
    {
        try
        {
            return System.Text.RegularExpressions.Regex.Matches(datos,
                @"(?<!Valor\s)Total(?:\s+filtrado)?:\s*\d+", System.Text.RegularExpressions.RegexOptions.IgnoreCase).Count;
        }
        catch { return 0; }
    }

    private static int ContarMarcasSuma(string datos)
    {
        try
        {
            return System.Text.RegularExpressions.Regex.Matches(datos,
                @"(?:Valor\s*total|importe\s*total|suma de la columna decimal)\s*:?\s*[\d,]+(?:\.\d+)?", System.Text.RegularExpressions.RegexOptions.IgnoreCase).Count;
        }
        catch { return 0; }
    }

    /// <summary>¿Todos los valores no vacíos de la columna son numéricos?</summary>
    private static bool EsColumnaNumerica(List<Dictionary<string, string>> filas, string columna)
        => filas.Select(f => f.TryGetValue(columna, out var v) ? v : "")
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .DefaultIfEmpty("x")
            .All(EsNumerico);

    /// <summary>
    /// Extrae el total exacto del header propio de SqlQueryTool
    /// ("... Total filtrado: 8." / "... Total: 30."). Contrato máquina-máquina.
    /// </summary>
    private static int? ExtraerTotalHeader(string datos)
    {
        // Excluye "Valor total:" (lookbehind). El punto final de frase ("Total: 8.")
        // sí se permite tras el entero.
        var m = System.Text.RegularExpressions.Regex.Match(
            datos, @"(?<!Valor\s)Total(?:\s+filtrado)?:\s*(\d+)(?![\d,])", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (m.Success && int.TryParse(m.Groups[1].Value, out var total) && total > 0)
            return total;
        return null;
    }

    private static string ObtenerString(Dictionary<string, object?> parametros, string clave, string fallback)
    {
        if (parametros.TryGetValue(clave, out var valor) && valor != null)
            return valor.ToString() ?? fallback;
        return fallback;
    }

    /// <summary>
    /// ¿El texto trae filas tabulares ('|' + ':')? Estructural, sin vocabulario:
    /// distingue una tabla real de la prosa con ':' sueltos.
    /// </summary>
    private static bool TieneFilasTabulares(string datos)
    {
        foreach (var linea in datos.Split('\n'))
        {
            var t = linea.Trim();
            if (t.Length == 0 || t.StartsWith("[Paso ", StringComparison.Ordinal)) continue;
            if (t.StartsWith("===", StringComparison.Ordinal)) continue;
            if (t.Count(c => c == '|') >= 1 && t.Contains(':'))
            {
                var partes = t.Split('|', StringSplitOptions.RemoveEmptyEntries);
                var campos = 0;
                foreach (var p in partes)
                {
                    var kv = p.Split(':', 2);
                    if (kv.Length == 2 && kv[0].Trim().Length > 0 && kv[1].Trim().Length > 0) campos++;
                }
                if (campos >= 2) return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Fusión mixta SQL + documental (plan #7060): totales exactos del header propio
    /// de SqlQueryTool + desglose por grupo + extracto documental limpio. Cada mitad
    /// se detecta por estructura (headers "[Paso N:]" y filas vs prosa), sin keywords.
    /// </summary>
    internal static string FormatearResumenMixto(string datos, string titulo)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"# {titulo}");
        sb.AppendLine();
        sb.AppendLine($"_Generado: {DateTime.Now:dd/MM/yyyy HH:mm}_");
        sb.AppendLine();

        var hayTotales = EscribirTotales(sb, datos);

        // Filas de SQL: en el resumen mixto se perdían y el informe quedaba sin los
        // datos que el resto del plan debe analizar (#1012/#1013).
        EscribirTablaDatos(sb, datos);

        // Desglose por grupo: líneas "- Col = Grupo: ..." ya calculadas en SQL.
        var desglose = datos.Split('\n')
            .Select(l => l.Trim())
            .Where(l => l.StartsWith("- ", StringComparison.Ordinal) && l.Contains('=') && l.Contains(':'))
            .Take(20)
            .ToList();
        if (desglose.Count > 0)
        {
            sb.AppendLine("## Datos estructurados");
            sb.AppendLine();
            foreach (var l in desglose) sb.AppendLine(l);
            sb.AppendLine();
        }

        // Eco del filtro SQL aplicado (va dentro de la línea "Datos obtenidos..."):
        // informa qué condición produjo las cifras, sin depender del wording.
        foreach (var filtro in datos.Split('\n')
            .Select(l =>
            {
                var i = l.IndexOf("Filtro aplicado:", StringComparison.OrdinalIgnoreCase);
                return i < 0 ? null : l[i..].Trim().TrimEnd('.');
            })
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(4))
            sb.AppendLine(filtro);

        sb.AppendLine("## Contexto documental");
        sb.AppendLine();
        var nFuentes = System.Text.RegularExpressions.Regex.Matches(datos, @"\[Fuente:").Count;
        if (nFuentes > 0)
        {
            sb.AppendLine($"**Fuentes consultadas:** {nFuentes}");
            sb.AppendLine();
        }
        var sinDatos = datos.Contains(ContratoResultado.PrefijoSinDatos, StringComparison.Ordinal);
        if (sinDatos && desglose.Count == 0 && !hayTotales)
        {
            sb.AppendLine(NotaSinDatos);
            sb.AppendLine();
        }
        foreach (var raw in datos.Split('\n'))
        {
            var t = raw.Trim();
            // Secciones SQL propias: no son prosa documental.
            if (t.StartsWith("[Paso ", StringComparison.Ordinal)) continue;
            if (t.StartsWith("Datos obtenidos", StringComparison.Ordinal)) continue;
            if (t.StartsWith(ContratoResultado.PrefijoSinDatos, StringComparison.Ordinal)) continue;
            // Nota de muestra del resumen SQL ("… (4 fila(s) más...)"): es pie de
            // tabla, no prosa documental. Marcador propio (empieza con "…").
            if (t.StartsWith("…", StringComparison.Ordinal) || t.StartsWith("...", StringComparison.Ordinal)) continue;
            if (t.StartsWith("- ", StringComparison.Ordinal) && t.Contains('=') && t.Contains(':')) continue;
            if (t.StartsWith("===", StringComparison.Ordinal) || t.StartsWith("INSTRUCCIÓN:")
                || t.StartsWith("- Copia") || t.StartsWith("- NO")
                || t.StartsWith("- Cita") || t.StartsWith("- Mantén")
                || t.StartsWith("Sección detectada:")) continue;
            if (t.Count(c => c == '|') >= 1 && t.Contains(':')) continue;
            if (t == "---") { sb.AppendLine(); continue; }
            if (t.Length == 0) { sb.AppendLine(); continue; }
            sb.AppendLine(raw.TrimEnd());
        }

        return sb.ToString();
    }

    /// <summary>
    /// Escribe las filas tabulares de SQL como tabla Markdown. Se usa en el resumen
    /// MIXTO (SQL + RAG): ese camino escribía totales y prosa documental pero se
    /// comía las filas, así que en el caso de aceptación el PDF salía sin los datos
    /// que luego debía analizar el paso de riesgos (plan #1012/#1013: el clasificador
    /// receives solo totales y respondió "sin riesgos" con riesgos presentes).
    /// Sin vocabulario de dominio: se apoya en el formato tabular '| campo: valor |'
    /// que emite SqlQueryTool.
    /// </summary>
    internal static void EscribirTablaDatos(System.Text.StringBuilder sb, string datos, int maxFilas = 12)
    {
        var filas = new List<Dictionary<string, string>>();
        foreach (var linea in datos.Split('\n'))
        {
            if (!linea.Contains('|') || !linea.Contains(':')) continue;
            var fila = new Dictionary<string, string>();
            foreach (var parte in linea.Split('|', StringSplitOptions.RemoveEmptyEntries))
            {
                var kv = parte.Split(':', 2);
                if (kv.Length == 2) fila[kv[0].Trim()] = kv[1].Trim();
            }
            if (fila.Count >= 2) filas.Add(fila);
        }
        if (filas.Count == 0) return;

        // La fila agregada ({Total, ValorTotal} todo numérico) no es un registro.
        var detalle = filas.Where(EsFilaDetalle).ToList();
        if (detalle.Count > 0) filas = detalle;
        if (filas.Count == 0) return;

        sb.AppendLine("## Datos obtenidos");
        sb.AppendLine();
        var columnas = filas.SelectMany(f => f.Keys).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        sb.AppendLine("| " + string.Join(" | ", columnas) + " |");
        sb.AppendLine("| " + string.Join(" | ", columnas.Select(_ => "---")) + " |");
        foreach (var fila in filas.Take(maxFilas))
            sb.AppendLine("| " + string.Join(" | ", columnas.Select(c => fila.TryGetValue(c, out var v) ? v : string.Empty)) + " |");
        if (filas.Count > maxFilas)
            sb.AppendLine($"… ({filas.Count - maxFilas} fila(s) más…)");
        sb.AppendLine();
    }

    /// <summary>
    /// ¿El texto trae marcadores propios de resultados (SQL, RAG o contrato)?
    /// Puerta de la entrega determinista: con estos marcadores el contenido se
    /// formatea con GenerarResumenEjecutivo sin pasar por el LLM. Todo son
    /// formatos máquina-máquina propios (headers de SqlQueryTool, secciones
    /// "[Paso N:]", token de ContratoResultado, marcas RAG), cero vocabulario.
    /// </summary>
    internal static bool EsResultadoEstructurado(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return false;
        if (texto.Contains(ContratoResultado.PrefijoSinDatos, StringComparison.Ordinal)) return true;
        if (EsContenidoDocumental(texto)) return true;
        if (TieneFilasTabulares(texto)) return true;
        if (texto.Contains("Datos obtenidos", StringComparison.Ordinal)) return true;
        if (texto.Contains("Desglose por", StringComparison.Ordinal)) return true;
        if (texto.Contains("Total filtrado:", StringComparison.OrdinalIgnoreCase)) return true;
        if (texto.Contains("Filtro aplicado:", StringComparison.OrdinalIgnoreCase)) return true;
        if (texto.Contains("[Paso ", StringComparison.Ordinal)) return true;
        return false;
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
    /// Nota legible que reemplaza una línea de contrato "sin datos" en salidas para
    /// el usuario. El token crudo nunca se muestra (plan #9066: entrega mixta con
    /// rama SQL vacía).
    /// </summary>
    private const string NotaSinDatos = "- Sin resultados de datos estructurados en esta consulta.";

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

        // El insumo documental puede traer un bloque SQL en formato desglose
        // (plan #9126: no hay filas tabulares, así que esta ruta documental recibe
        // también el texto "Datos obtenidos... Desglose por Ciudad..."). Se
        // delega el mismo criterio que la ruta mixta: un solo total del header
        // propio de SqlQueryTool, o el desglose por tabla cuando hay varias.
        // Con varias marcas de un mismo grupo no se inventa un gran total: cada
        // grupo ya muestra su cifra. Sin header no se inventa nada.
        int? totalHeader; decimal? sumaHeader;
        var porTabla = ExtraerTotalesPorTabla(datos);
        if (porTabla.Count > 1)
        {
            sb.AppendLine("## Totales por tabla");
            sb.AppendLine();
            foreach (var (etiqueta, total, suma) in porTabla)
            {
                var partes = new List<string>();
                if (total is > 0) partes.Add($"{total.Value:F0} registros");
                if (suma.HasValue) partes.Add($"valor {suma.Value:N2}");
                sb.AppendLine($"- **{etiqueta}**: {(partes.Count > 0 ? string.Join(" · ", partes) : "sin totales")}");
            }
            sb.AppendLine();
            totalHeader = null; sumaHeader = null;
        }
        else
        {
            // Varias marcas de total en el MISMO texto pueden ser dos cosas: varios
            // grupos de una respuesta (no se puede sumar) o varias SECCIONES [Paso N]
            // de la misma tabla (sí se suman, y ExtraerTotalesPorSeccion ya lo hizo).
            // Antes el guard `== 1` silenciaba ambas y dos ramas paralelas de la
            // misma tabla perdían sus totales.
            var porSecciones = TieneSeccionesDePasos(datos);
            (totalHeader, sumaHeader) = ExtraerTotalesPorSeccion(datos);
            if (totalHeader is > 0 && (ContarMarcasTotal(datos) == 1 || porSecciones))
                sb.AppendLine($"**Total de registros (SQL):** {totalHeader.Value:F0}");
            else totalHeader = null;
            if (sumaHeader.HasValue && (ContarMarcasSuma(datos) == 1 || porSecciones))
                sb.AppendLine($"**Valor total (SQL):** {sumaHeader.Value:N2}");
            else sumaHeader = null;
            if (totalHeader is > 0 || sumaHeader.HasValue)
                sb.AppendLine();
            else if (nFuentes == 0)
                sb.AppendLine();
        }

        var sinDatos = false;
        foreach (var raw in datos.Split('\n'))
        {
            var linea = raw.TrimEnd();
            var t = linea.Trim();
            if (t.StartsWith(ContratoResultado.PrefijoSinDatos, StringComparison.Ordinal)) { sinDatos = true; continue; }
            if (t.StartsWith("===") || t.StartsWith("INSTRUCCIÓN:")
                || t.StartsWith("- Copia") || t.StartsWith("- NO")
                || t.StartsWith("- Cita") || t.StartsWith("- Mantén")
                || t.StartsWith("Sección detectada:")) continue;
            if (t == "---") { sb.AppendLine(); continue; }
            sb.AppendLine(linea);
        }
        if (sinDatos)
        {
            sb.AppendLine();
            sb.AppendLine(NotaSinDatos);
        }

        return sb.ToString();
    }

    /// <summary>
    /// ¿Hay datos REALES en el texto? Antes buscaba literales ("total filtrado:",
    /// "total:") que dependían del formato exacto con que otra herramienta redactaba su
    /// salida: cambiar ese formato hacía que el ReportTool concluyera que no hay datos y
    /// no generara nada, sin ningún error visible.
    ///
    /// Ahora se decide por estructura: un resultado de datos tiene filas con campos
    /// separados por '|' o separadores "clave: valor". No depende del vocabulario con que
    /// otro componente escriba.
    /// </summary>
    private static bool TieneDatosValidos(string datos)
    {
        if (string.IsNullOrWhiteSpace(datos)) return false;
        if (ContratoResultado.EsSinDatos(datos) && ContratoResultado.TodosSonSinDatos(datos))
            return false;

        // Estructura de fila: varios campos separados por '|'.
        foreach (var linea in datos.Split('\n'))
        {
            var t = linea.Trim();
            if (t.Length == 0) continue;
            if (t.StartsWith(ContratoResultado.PrefijoSinDatos, StringComparison.Ordinal)) continue;
            if (t.Count(c => c == '|') >= 1 && t.Contains(':')) return true;
        }
        return false;
    }

    /// <summary>
    /// ¿El texto consiste SOLO en marcadores de "sin datos"? Se decide por el CONTRATO
    /// (ContratoResultado), no comparando frases: antes se列表aban a mano los mensajes
    /// de error de cada herramienta y la lista se desincronizaba (si el wording cambiaba,
    /// el reporte salía vacío).
    /// </summary>
    private static bool TodosSonMarcadoresSinDatos(string datos)
        => ContratoResultado.TodosSonSinDatos(datos);

    /// <summary>
    /// Detecta los textos marcadores que las herramientas de consulta devuelven cuando no
    /// hay información real. Se decide por el CONTRATO compartido, no comparando las
    /// frases de error de cada herramienta (esa lista se desincronizaba).
    /// </summary>
    private static bool EsMarcadorSinDatos(string datos)
        => ContratoResultado.TodosSonSinDatos(datos);
}
