using System;
using System.Collections.Generic;
using System.Linq;

namespace Asistente.Application.Services.Herramientas;

/// <summary>
/// Valor observado en BD que empareja con la pregunta.
/// </summary>
/// <summary>
/// Valor observado que empareja con la pregunta. <paramref name="Clave"/> es la
/// clave completa "Tabla.Columna" del mapa de origen: sin ella el consumidor solo
/// sabía la columna y no podía saber de qué tabla era (y con dos tablas que
/// comparten nombre de columna elegía la equivocada).
/// </summary>
public sealed record ValorDetectado(string Columna, string Literal, bool EsCompleto, int Longitud, string Clave = "");

/// <summary>
/// Detección de filtros desde valores OBSERVADOS en BD (mapa Tabla.Col → valores).
/// Sin vocabulario fijo: emparejamiento por palabra completa, morfología genérica
/// (plural -s/-es, alternancia de género) y subsecuencia contigua para valores
/// multipalabra. Compartido por SqlQueryTool (WHERE) y PlanBuilder (ramas).
/// </summary>
public static class FiltroSemantico
{
    /// <summary>Minúsculas sin acentos (normalización Unicode, sin recortes).</summary>
    public static string Normalizar(string texto)
        => string.IsNullOrEmpty(texto) ? string.Empty : new string(texto
            .ToLowerInvariant()
            .Normalize(System.Text.NormalizationForm.FormD)
            .Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark)
            .ToArray());

    /// <summary>Palabras significativas de la pregunta (normalizadas, len ≥ 3).</summary>
    public static List<string> Palabras(string pregunta)
    {
        if (string.IsNullOrWhiteSpace(pregunta)) return new();
        return Normalizar(pregunta)
            .Split(new[] { ' ', '\t', '\r', '\n', '?', '¿', '.', ',', ';', ':', '(', ')', '"' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(w => w.Trim('\'')).Where(w => w.Length >= 3).ToList();
    }

    /// <summary>Nombre de columna desde clave "Tabla.Col".</summary>
    public static string ColumnaDe(string claveTablaCol)
        => claveTablaCol.Contains('.')
            ? claveTablaCol[(claveTablaCol.LastIndexOf('.') + 1)..].Trim().Trim('[', ']')
            : claveTablaCol.Trim().Trim('[', ']');

    /// <summary>
    /// Detecta candidatos exactos (palabra completa / frase / subsecuencia) y
    /// plurales (morfología) por columna. No filtra ni agrupa: ver ElegirPorColumna.
    /// </summary>
    public static void Detectar(
        string pregunta,
        Dictionary<string, List<string>> mapaValores,
        List<ValorDetectado> exactos,
        List<ValorDetectado> plurales)
    {
        try
        {
            var palabras = Palabras(pregunta);
            if (palabras.Count == 0) return;

            foreach (var kv in mapaValores)
            {
                var col = ColumnaDe(kv.Key);
                if (string.IsNullOrWhiteSpace(col)) continue;
                foreach (var valor in kv.Value)
                {
                    var valNorm = Normalizar(valor).Trim();
                    if (valNorm.Length < 3) continue;

                    var valWordsNorm = valNorm.Split([' ', '\t', '-', '/'], StringSplitOptions.RemoveEmptyEntries);
                    if (valWordsNorm.Length > 1)
                    {
                        // Valor multipalabra: frase completa o subsecuencia contigua
                        // ("oficina lima" ⊂ "Oficina Lima - Piso 3").
                        if (ContieneSecuencia(palabras, valWordsNorm))
                        {
                            exactos.Add(new(col, valor.Trim(), true, valNorm.Length, kv.Key));
                        }
                        else
                        {
                            var valWordsOrig = valor.Trim()
                                .Split([' ', '\t', '-', '/'], StringSplitOptions.RemoveEmptyEntries);
                            for (int len = valWordsNorm.Length - 1; len >= 2 && valWordsOrig.Length == valWordsNorm.Length; len--)
                            {
                                var hallado = false;
                                for (int i = 0; i + len <= valWordsNorm.Length; i++)
                                {
                                    var sub = valWordsNorm.Skip(i).Take(len).ToArray();
                                    if (!sub.Any(w => w.Length >= 4)) continue;
                                    if (ContieneSecuencia(palabras, sub))
                                    {
                                        var lit = string.Join(" ", valWordsOrig.Skip(i).Take(len));
                                        exactos.Add(new(col, lit, false, lit.Length, kv.Key));
                                        hallado = true;
                                        break;
                                    }
                                }
                                if (hallado) break;
                            }
                        }
                        continue;
                    }

                    if (palabras.Any(w => w == valNorm))
                    {
                        exactos.Add(new(col, valor.Trim(), true, valNorm.Length, kv.Key));
                    }
                    else if (palabras.Any(w => EsFormaDe(w, valNorm)))
                    {
                        plurales.Add(new(col, valor.Trim(), true, valNorm.Length, kv.Key));
                    }
                }
            }
        }
        catch { /* sin candidatos */ }
    }

    /// <summary>
    /// Elige valores finales por columna: en cada columna ganan los exactos sobre
    /// los plurales; supresión de sombras (INACTIVO ⊃ ACTIVO descarta el corto);
    /// tope 4 por columna y 2 columnas. Sin vocabulario fijo.
    ///
    /// La preferencia es POR COLUMNA, no global (plan #9121): "productos entregados
    /// en Lima" tiene 'Lima' exacto en Ciudad y 'entregados' plural de 'Entregado'
    /// en Estado. Antes, una sola coincidencia exacta en cualquier columna apagaba
    /// todas las plurales y el filtro de estado se perdía
    /// (WHERE [Ciudad]='Lima' sin WHERE [Estado]='Entregado').
    /// </summary>
    public static Dictionary<string, List<ValorDetectado>> ElegirPorColumna(
        List<ValorDetectado> exactos, List<ValorDetectado> plurales,
        int maxPorColumna = 4, int maxColumnas = 2)
    {
        var resultado = new Dictionary<string, List<ValorDetectado>>(StringComparer.OrdinalIgnoreCase);
        try
        {
            // Columnas con coincidencia exacta; el resto se resuelve con plurales.
            var columnasConExactos = new HashSet<string>(
                exactos.Select(x => x.Columna), StringComparer.OrdinalIgnoreCase);
            var candidatos = exactos
                .Concat(plurales.Where(p => !columnasConExactos.Contains(p.Columna)))
                .ToList();

            foreach (var grupo in candidatos.GroupBy(x => x.Columna, StringComparer.OrdinalIgnoreCase).Take(maxColumnas))
            {
                var kept = new List<ValorDetectado>();
                foreach (var c in grupo.OrderByDescending(x => x.Longitud))
                {
                    var normC = Normalizar(c.Literal);
                    if (kept.Any(k => Normalizar(k.Literal).Contains(normC, StringComparison.Ordinal)))
                        continue;
                    if (kept.Any(k => k.Literal.Equals(c.Literal, StringComparison.OrdinalIgnoreCase)))
                        continue;
                    kept.Add(c);
                    if (kept.Count >= maxPorColumna) break;
                }
                if (kept.Count > 0) resultado[grupo.Key] = kept;
            }
        }
        catch { }
        return resultado;
    }

    /// <summary>¿La secuencia aparece contigua en las palabras? (comparación ordinal).</summary>
    public static bool ContieneSecuencia(List<string> palabras, string[] secuencia)
    {
        if (secuencia.Length == 0 || palabras.Count < secuencia.Length) return false;
        for (int i = 0; i + secuencia.Length <= palabras.Count; i++)
        {
            var ok = true;
            for (int j = 0; j < secuencia.Length; j++)
                if (!palabras[i + j].Equals(secuencia[j], StringComparison.Ordinal)) { ok = false; break; }
            if (ok) return true;
        }
        return false;
    }

    /// <summary>
    /// ¿La palabra es forma (plural/género) del valor observado? Morfología
    /// española genérica: sufijos -s/-es y alternancia -o/-a. Sin vocabulario.
    /// </summary>
    public static bool EsFormaDe(string palabra, string valor)
    {
        if (palabra.StartsWith(valor + "s", StringComparison.Ordinal) ||
            palabra.StartsWith(valor + "es", StringComparison.Ordinal))
            return true;
        foreach (var tallo in Tallos(palabra))
            if (tallo.Equals(valor, StringComparison.Ordinal))
                return true;
        return false;
    }

private static IEnumerable<string> Tallos(string palabra)
    {
        var bases = new HashSet<string>(StringComparer.Ordinal) { palabra };
        if (palabra.EndsWith("es", StringComparison.Ordinal) && palabra.Length > 4)
            bases.Add(palabra[..^2]);
        if (palabra.EndsWith("s", StringComparison.Ordinal) && palabra.Length > 3)
            bases.Add(palabra[..^1]);
        // Alternancia de g�nero sobre cada base (entregada?entregado).
        foreach (var b in bases.ToList())
        {
            if (b.EndsWith("a", StringComparison.Ordinal) && b.Length > 3)
                bases.Add(b[..^1] + "o");
            else if (b.EndsWith("o", StringComparison.Ordinal) && b.Length > 3)
                bases.Add(b[..^1] + "a");
        }
        return bases;
    }

    /// <summary>
    /// Similitud Jaro-Winkler entre dos cadenas (0-1). Métrica de edición pura,
    /// sin vocabulario: sirve para tolerar typos ("sotenibilidad" vs
    /// "sostenibilidad") al comparar contra datos vivos del catálogo.
    /// </summary>
    public static double JaroWinkler(string a, string b)
    {
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return 0;
        if (a.Equals(b, StringComparison.Ordinal)) return 1;
        var s1 = a;
        var s2 = b;
        if (s1.Length > s2.Length) (s1, s2) = (s2, s1);
        var len1 = s1.Length;
        var len2 = s2.Length;
        if (len1 == 0) return 0;
        var radio = Math.Max(len2 / 2 - 1, 0);
        var emparejados1 = new bool[len1];
        var emparejados2 = new bool[len2];
        var coincidencias = 0;
        for (var i = 0; i < len1; i++)
        {
            var desde = Math.Max(i - radio, 0);
            var hasta = Math.Min(i + radio + 1, len2);
            for (var j = desde; j < hasta; j++)
            {
                if (!emparejados2[j] && s1[i] == s2[j])
                {
                    emparejados1[i] = true;
                    emparejados2[j] = true;
                    coincidencias++;
                    break;
                }
            }
        }
        if (coincidencias == 0) return 0;
        var transposiciones = 0;
        var k = 0;
        for (var i = 0; i < len1; i++)
        {
            if (!emparejados1[i]) continue;
            while (!emparejados2[k]) k++;
            if (s1[i] != s2[k]) transposiciones++;
            k++;
        }
        var jaro = (coincidencias / (double)len1
            + coincidencias / (double)len2
            + (coincidencias - transposiciones / 2.0) / coincidencias) / 3.0;
        var prefijo = 0;
        for (var i = 0; i < Math.Min(4, Math.Min(len1, len2)); i++)
        {
            if (s1[i] == s2[i]) prefijo++;
            else break;
        }
        return jaro + prefijo * 0.1 * (1 - jaro);
    }

    /// <summary>
    /// Tokens alfanuméricos normalizados de un texto (para comparar nombres de
    /// archivo/códigos por partes: "sostenibilidad_v1.pdf" → {sostenibilidad}).
    /// Sin listas: separa por estructura y filtra por longitud mínima.
    /// </summary>
    public static List<string> TokensAlfanumericos(string texto, int longitudMinima = 5)
    {
        var resultado = new List<string>();
        if (string.IsNullOrWhiteSpace(texto)) return resultado;
        foreach (var parte in System.Text.RegularExpressions.Regex.Split(Normalizar(texto), @"[^a-z0-9]+"))
        {
            var t = parte.Trim();
            if (t.Length >= longitudMinima && !resultado.Contains(t, StringComparer.Ordinal))
                resultado.Add(t);
        }
        return resultado;
    }
}
