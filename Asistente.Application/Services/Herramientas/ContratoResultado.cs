using System;

namespace Asistente.Application.Services.Herramientas;

/// <summary>
/// CONTRATO ÚNICO para "esta herramienta no trajo datos".
///
/// El problema que resuelve: cada consumidor reconocía el "sin datos" copiando el TEXTO
/// del mensaje de error de cada herramienta. Eso:
///
///   - se desincronizaba solo (ReportTool y AgentOrchestrator tenían listas distintas);
///   - dependía de la redacción: cambiar "la consulta no devolvió registros" por otra
///     frase hacía que la herramienta decidiera que SÍ tenía datos y generara un reporte
///     vacío, o que el agente final delivera una tabla inventada;
///   - exigía actualizar varios archivos por cada mensaje nuevo.
///
/// Ahora hay UN token compartido. Quien produce lo emite; quien consume lo reconoce por
/// el símbolo, nunca por comparar prosa. La prosa legible la pone el que la consume.
///
/// Nota: un marcador de contrato es un valor de protocolo, no vocabulario de negocio.
/// No depende de la base de datos ni del dominio.
/// </summary>
public static class ContratoResultado
{
    /// <summary>Prefijo de línea que declara "sin datos". Un solo símbolo compartido.</summary>
    public const string PrefijoSinDatos = "[SIN_DATOS]";

    /// <summary>Envolve un resultado para declarar explícitamente que no hay datos.</summary>
    public static string MarcarSinDatos(string? motivo = null)
        => string.IsNullOrWhiteSpace(motivo) ? PrefijoSinDatos : $"{PrefijoSinDatos} {motivo.Trim()}";

    /// <summary>¿El texto declara que no hay datos?</summary>
    public static bool EsSinDatos(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return true; // nada recibido = nada que entregar
        foreach (var linea in texto.Split('\n'))
        {
            var t = linea.Trim();
            if (t.Length == 0) continue;
            return t.StartsWith(PrefijoSinDatos, StringComparison.Ordinal);
        }
        return false;
    }

    /// <summary>Quita las líneas de contrato para dejar la prosa legible.</summary>
    public static string SinMarcadores(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return texto ?? string.Empty;
        var lineas = texto.Split('\n')
            .Where(l => !l.TrimStart().StartsWith(PrefijoSinDatos, StringComparison.Ordinal))
            .ToList();
        return string.Join("\n", lineas).Trim();
    }

    /// <summary>
    /// ¿Todas las líneas de un resultado son "sin datos"? Un resultado mixto (datos
    /// reales + una rama vacía) NO es "sin datos": se entrega lo que hay.
    /// </summary>
    public static bool TodosSonSinDatos(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return true;
        var lineas = texto.Split('\n')
            .Select(l => l.Trim())
            .Where(l => l.Length > 0)
            .ToList();
        if (lineas.Count == 0) return true;
        return lineas.All(l => l.StartsWith(PrefijoSinDatos, StringComparison.Ordinal));
    }
}
