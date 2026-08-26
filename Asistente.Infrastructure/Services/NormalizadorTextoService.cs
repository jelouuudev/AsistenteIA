using System.Text;
using System.Text.RegularExpressions;
using Asistente.Domain.Interfaces;

namespace Asistente.Infrastructure.Services;

public class NormalizadorTextoService : INormalizadorTextoService
{
    private static readonly Regex RegexMultipleEspacios = new(@"[^\S\n]+", RegexOptions.Compiled);
    private static readonly Regex RegexSaltosLineaMultiples = new(@"(\r?\n){3,}", RegexOptions.Compiled);
    private static readonly Regex RegexCaracteresControl = new(@"[\x00-\x08\x0B\x0C\x0E-\x1F\x7F]", RegexOptions.Compiled);
    private static readonly Regex RegexCaracteresNoLatino = new(
        @"[⺀-⻿　-〿぀-ヿ㐀-䶿一-鿿豈-﫿＀-￯㇀-㇯]",
        RegexOptions.Compiled);
    private static readonly Regex RegexEspaciosInicioLinea = new(@"^[ \t]+", RegexOptions.Multiline | RegexOptions.Compiled);
    private static readonly Regex RegexEspaciosFinLinea = new(@"[ \t]+$", RegexOptions.Multiline | RegexOptions.Compiled);
    private static readonly Regex RegexPuntuacionMultiple = new(@"([.!?]){2,}", RegexOptions.Compiled);

    public string Normalizar(string texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return string.Empty;

        var sb = new StringBuilder(texto);

        // Eliminar caracteres de control
        var resultado = RegexCaracteresControl.Replace(sb.ToString(), string.Empty);

        // Eliminar caracteres no latinos (chino, japonés, caracteres de ancho completo, etc.)
        // que suelen provenir de OCR defectuoso en PDF escaneados.
        resultado = RegexCaracteresNoLatino.Replace(resultado, string.Empty);

        // Normalizar saltos de línea (Windows/Mac/Linux)
        resultado = resultado.Replace("\r\n", "\n").Replace("\r", "\n");

        // Unificar saltos de línea múltiples (preservar doble salto para párrafos)
        resultado = RegexSaltosLineaMultiples.Replace(resultado, "\n\n");

        // Eliminar espacios innecesarios al inicio de línea
        resultado = RegexEspaciosInicioLinea.Replace(resultado, string.Empty);

        // Eliminar espacios innecesarios al final de línea
        resultado = RegexEspaciosFinLinea.Replace(resultado, string.Empty);

        // Reemplazar múltiples espacios por uno solo
        resultado = RegexMultipleEspacios.Replace(resultado, " ");

        // Reducir puntuación excesiva
        resultado = RegexPuntuacionMultiple.Replace(resultado, "$1$1");

        // Preservar estructura de párrafos (doble salto)
        resultado = resultado.Replace("\n\n", "\n\n");

        // Eliminar espacios antes de saltos de línea
        resultado = Regex.Replace(resultado, @"[ \t]+\n", "\n");

        // Eliminar espacios después de saltos de línea al inicio
        resultado = Regex.Replace(resultado, @"\n[ \t]+", "\n");

        // Normalizar espacios múltiples restantes
        resultado = Regex.Replace(resultado, @" {2,}", " ");

        // Reunir identificadores de código divididos por saltos de línea
        // (p.ej. "User\nProfile" -> "UserProfile", "_database\nConnection" -> "_databaseConnection")
        resultado = Regex.Replace(resultado, @"(?<=[a-záéíóúñü])[ \t]*\n[ \t]*(?=[A-ZÁÉÍÓÚÑ])", string.Empty);

        // Reunir siglas separadas por salto de línea del punto previo (p.ej. ".NET")
        resultado = Regex.Replace(resultado, @"(?<=\.)[ \t]*\n[ \t]*(?=[A-ZÁÉÍÓÚÑ]{2,})", string.Empty);

        // Reunir palabras cortadas con guion al final de línea (p.ej. "User-\nProfile")
        resultado = Regex.Replace(resultado, @"-[ \t]*\n[ \t]*", string.Empty);

        // Trim final
        resultado = resultado.Trim();

        // Asegurar codificación UTF-8 limpia
        var bytes = Encoding.UTF8.GetBytes(resultado);
        resultado = Encoding.UTF8.GetString(bytes);

        return resultado;
    }
}
