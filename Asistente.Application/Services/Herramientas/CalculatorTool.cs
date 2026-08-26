using System;
using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Asistente.Domain.Interfaces;

namespace Asistente.Application.Services.Herramientas;

/// <summary>
/// Herramienta de utilidad que evalúa expresiones matemáticas de forma segura
/// (sin acceso a servicios externos). Soporta +, -, *, /, %, paréntesis y funciones básicas.
/// </summary>
public class CalculatorTool : ITool
{
    public string Name => "CalculatorTool";
    public string Description =>
        "Realiza operaciones matemáticas. Parámetro 'expresion' (ej. \"(120 + 80) * 1.18\"). " +
        "Soporta +, -, *, /, % y paréntesis. No tiene acceso a servicios externos.";
    public string Categoria => "Utilidad";

    public Task<ToolExecutionResult> ExecuteAsync(ToolExecutionRequest request, CancellationToken cancellationToken = default)
    {
        var expr = ObtenerString(request.Parametros, "expresion", request.PreguntaOriginal ?? string.Empty);
        if (string.IsNullOrWhiteSpace(expr))
            return Task.FromResult(new ToolExecutionResult { Exitoso = false, Error = "No se proporcionó una expresión." });

        try
        {
            var resultado = Evaluar(expr);
            return Task.FromResult(new ToolExecutionResult
            {
                Exitoso = true,
                Contenido = $"{expr} = {resultado}",
                Metadatos = new() { ["resultado"] = resultado }
            });
        }
        catch (Exception ex)
        {
            return Task.FromResult(new ToolExecutionResult { Exitoso = false, Error = $"Expresión inválida: {ex.Message}" });
        }
    }

    private static double Evaluar(string expresion)
    {
        var tabla = new DataTable();
        // Validación de seguridad: solo caracteres permitidos
        foreach (var c in expresion)
        {
            if (!char.IsDigit(c) && !"+()-*/.% ,".Contains(c) && c != '.')
                throw new InvalidExpressionException($"Carácter no permitido: {c}");
        }
        var columna = tabla.Columns.Add("calc", typeof(double));
        tabla.Rows.Add(tabla.NewRow());
        columna.Expression = expresion.Replace(",", ".");
        return Convert.ToDouble(tabla.Rows[0]["calc"]);
    }

    private static string ObtenerString(Dictionary<string, object?> parametros, string clave, string fallback)
    {
        if (parametros.TryGetValue(clave, out var valor) && valor != null)
            return valor.ToString() ?? fallback;
        return fallback;
    }
}
