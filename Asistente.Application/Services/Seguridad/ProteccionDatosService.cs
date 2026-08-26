using System.Text.RegularExpressions;
using Asistente.Application.Interfaces;

namespace Asistente.Application.Services.Seguridad;

/// <summary>
/// Enmascara información sensible para que nunca se envíe al modelo (Actividad 10 / Regla 8).
/// Ejemplos configurables: DNI, contraseña, token, número de tarjeta, credenciales.
/// </summary>
public class ProteccionDatosService : IProteccionDatosService
{
    // Patrones de datos sensibles (se pueden extender vía configuración).
    private static readonly (Regex Regex, string Etiqueta)[] Patrones = new[]
    {
        (new Regex(@"\b(?:\d[ -]*?){13,19}\b", RegexOptions.Compiled), "TARJETA"),
        (new Regex(@"(?i)(token|api[_-]?key|secret|password|contraseña|credential)\s*[:=]\s*\S+", RegexOptions.Compiled), "CREDENCIAL"),
        (new Regex(@"\b\d{8}[A-Za-z]\b", RegexOptions.Compiled), "DNI"),
        (new Regex(@"(?i)\b(?:Bearer\s+)[A-Za-z0-9._-]+\b", RegexOptions.Compiled), "TOKEN")
    };

    public bool ContieneSensible(string texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return false;
        foreach (var (rx, _) in Patrones)
            if (rx.IsMatch(texto)) return true;
        return false;
    }

    public string Enmascarar(string texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return texto;
        foreach (var (rx, etiqueta) in Patrones)
            texto = rx.Replace(texto, $"[{etiqueta}_REDACTADO]");
        return texto;
    }
}
