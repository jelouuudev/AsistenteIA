using System.Text.RegularExpressions;
using Asistente.Application.Interfaces;

namespace Asistente.Application.Services.Seguridad;

/// <summary>
/// Protección contra Prompt Injection (Actividad 11 / Reglas 3 y 5).
/// Detecta instrucciones que intentan reemplazar el System Prompt, otorgar permisos
/// o modificar políticas de seguridad desde documentos/mensajes.
/// </summary>
public class PromptInjectionService : IPromptInjectionService
{
    // Patrones de intento de manipulación del sistema (multilingüe: español/inglés).
    private static readonly Regex[] PatronesMaliciosos =
    {
        new(@"(ignore|ignora|ignora(?:s|r)?\s+(las|tus|previous|all|above))\s+(previous|all|above|instructions|instrucciones|prompt)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"(system\s*prompt|system\s*instruction|instrucci[oó]n\s*(del\s*)?sistema)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"(you\s+are\s+now|act\s+as\s+if|pretend\s+to\s+be|haz\s+como\s+si|comp[oó]rtate\s+como)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"(grant\s+(yourself|admin|administrator)\s+permissions?|elevate\s+(your\s+)?(privileges|permissions?)|otorga(?:r)?\s+(permisos|acceso)\s+(de\s+)?(admin|administrador)|eleva(?:r)?\s+tus\s+privilegios)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"(override|disable|bypass|ignore|anula|desactiva|ignora)\s+(safety|security|policy|restriction|rule|seguridad|pol[ií]tica|restricci[oó]n|regla)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"(forget\s+(your|the)\s+(instructions|rules|guidelines)|olvida\s+(tus|las)\s+(instrucciones|reglas))", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"(system\s*override|override\s+(del\s*)?sistema)", RegexOptions.IgnoreCase | RegexOptions.Compiled)
    };

    public bool EsMalicioso(string contenido, out string razon)
    {
        razon = string.Empty;
        if (string.IsNullOrWhiteSpace(contenido)) return false;
        foreach (var rx in PatronesMaliciosos)
        {
            var m = rx.Match(contenido);
            if (m.Success)
            {
                razon = $"Patrón de manipulación detectado: '{m.Value}'.";
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Separa claramente el contenido recuperado (RAG) del sistema y neutraliza
    /// instrucciones embebidas para que no otorguen permisos ni cambien políticas.
    /// Las porciones que coincidan con patrones de manipulación se reemplazan por un marcador.
    /// </summary>
    public string SanitizarContenidoRecuperado(string contenido)
    {
        if (string.IsNullOrWhiteSpace(contenido)) return contenido;
        var resultado = contenido;
        foreach (var rx in PatronesMaliciosos)
            resultado = rx.Replace(resultado, "[INSTRUCCIÓN NEUTRALIZADA]");
        return resultado;
    }
}
