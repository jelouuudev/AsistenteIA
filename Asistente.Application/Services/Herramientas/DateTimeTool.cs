using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Asistente.Domain.Interfaces;

namespace Asistente.Application.Services.Herramientas;

/// <summary>
/// Herramienta de utilidad que devuelve la fecha y hora actual del servidor.
/// </summary>
public class DateTimeTool : ITool
{
    public string Name => "DateTimeTool";
    public string Description =>
        "Obtiene la fecha y hora actual del servidor. Parámetro opcional 'formato' " +
        "(ej. \"dd/MM/yyyy\", \"dddd\" en inglés). Úsala cuando el usuario pregunte por la fecha u hora actual.";
    public string Categoria => "Utilidad";

    public Task<ToolExecutionResult> ExecuteAsync(ToolExecutionRequest request, CancellationToken cancellationToken = default)
    {
        var formato = ObtenerString(request.Parametros, "formato", "dd/MM/yyyy HH:mm:ss");
        var ahora = DateTime.Now;
        var resultado = ahora.ToString(formato, CultureInfo.InvariantCulture);

        return Task.FromResult(new ToolExecutionResult
        {
            Exitoso = true,
            Contenido = $"Fecha y hora actual del servidor: {resultado}",
            Metadatos = new()
            {
                ["fechaHora"] = ahora.ToString("yyyy-MM-ddTHH:mm:ss"),
                ["fecha"] = ahora.ToString("dd/MM/yyyy"),
                ["hora"] = ahora.ToString("HH:mm:ss")
            }
        });
    }

    private static string ObtenerString(Dictionary<string, object?> parametros, string clave, string fallback)
    {
        if (parametros.TryGetValue(clave, out var valor) && valor != null)
            return valor.ToString() ?? fallback;
        return fallback;
    }
}
