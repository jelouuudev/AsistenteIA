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

        // ETAPA 19.1: generar resumen ejecutivo con métricas agrupadas (conteos, totales, conclusión).
        var resumen = GenerarResumenEjecutivo(datos, titulo);

        return Task.FromResult(new ToolExecutionResult
        {
            Exitoso = true,
            Contenido = resumen,
            Metadatos = new() { ["titulo"] = titulo, ["lineas"] = datos.Split('\n').Length }
        });
    }

    /// <summary>
    /// ETAPA 19.2: genera un resumen ejecutivo con métricas agrupadas a partir de datos en formato
    /// "Campo: Valor | Campo2: Valor2". Detecta campos repetidos (ej. Estado), suma los valores
    /// numéricos (Cantidad/ValorTotal) y produce una conclusión con porcentajes reales.
    /// </summary>
    private static string GenerarResumenEjecutivo(string datos, string titulo)
    {
        var lineas = datos.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .Where(l => !l.StartsWith("Datos obtenidos") && !l.StartsWith("==") && l.Contains(':'))
            .ToList();

        if (lineas.Count == 0)
        {
            var sbSimple = new System.Text.StringBuilder();
            sbSimple.AppendLine($"# {titulo}");
            sbSimple.AppendLine();
            sbSimple.AppendLine($"_Generado: {DateTime.Now:dd/MM/yyyy HH:mm}_");
            sbSimple.AppendLine();
            sbSimple.AppendLine(datos);
            return sbSimple.ToString();
        }

        // Parsear filas en diccionarios.
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
            var sbSimple = new System.Text.StringBuilder();
            sbSimple.AppendLine($"# {titulo}");
            sbSimple.AppendLine();
            sbSimple.AppendLine($"_Generado: {DateTime.Now:dd/MM/yyyy HH:mm}_");
            sbSimple.AppendLine();
            sbSimple.AppendLine(datos);
            return sbSimple.ToString();
        }

        // Detectar campo de agrupación (Estado/Tipo/Categoría).
        var campoAgrupacion = filas[0].Keys.FirstOrDefault(k =>
            k.Equals("Estado", StringComparison.OrdinalIgnoreCase) ||
            k.Equals("Tipo", StringComparison.OrdinalIgnoreCase) ||
            k.Equals("Categoria", StringComparison.OrdinalIgnoreCase) ||
            k.Equals("Categoría", StringComparison.OrdinalIgnoreCase));

        // Detectar campo numérico para sumar (Cantidad/ValorTotal/Precio/Monto).
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

        // Calcular totales reales sumando los campos numéricos.
        double totalGeneral = 0;
        double totalValor = 0;
        foreach (var fila in filas)
        {
            if (campoCantidad != null && fila.TryGetValue(campoCantidad, out var cStr) && double.TryParse(cStr.Replace(",", ""), out var cVal))
                totalGeneral += cVal;
            if (campoValor != null && fila.TryGetValue(campoValor, out var vStr) && double.TryParse(vStr.Replace(",", ""), out var vVal))
                totalValor += vVal;
        }

        // Si no hay campo numérico, contar filas.
        if (totalGeneral == 0) totalGeneral = filas.Count;

        reporte.AppendLine($"**Total de registros:** {totalGeneral:F0}");
        if (totalValor > 0)
            reporte.AppendLine($"**Valor total:** {totalValor:N2}");
        reporte.AppendLine();

        // Tabla de detalle.
        reporte.AppendLine("| Campo | Valor |");
        reporte.AppendLine("| --- | --- |");
        foreach (var fila in filas)
        {
            var campo = campoAgrupacion != null && fila.TryGetValue(campoAgrupacion, out var g) ? g : fila.Keys.First();
            var valor = campoCantidad != null && fila.TryGetValue(campoCantidad, out var c) ? c : fila.Values.First().ToString();
            reporte.AppendLine($"| {campo} | {valor} |");
        }

        // Conclusión con porcentajes reales.
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
                        cantidadGrupo += 1; // fallback: contar fila
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
}
