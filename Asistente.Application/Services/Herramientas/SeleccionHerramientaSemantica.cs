using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Asistente.Domain.Entities;
using Asistente.Domain.Interfaces;

namespace Asistente.Application.Services.Herramientas;

/// <summary>
/// Decide si un mensaje pide una herramienta comparándolo con la DESCRIPCIÓN de cada
/// herramienta disponible (dato de configuración en la BD), no con una lista de
/// palabras.
///
/// Por qué: el proyecto se conecta a varias bases de datos, incluidas nuevas. Una lista
/// de términos ("cliente", "producto", "activo", "pedido") solo describe el negocio
/// actual: contra otra base el disparo nunca ocurre, y contra esta dispara con textos
/// que no piden datos ("muéstrame los productos de la cocina"). Agregar una base
/// obligaba a editar la lista en C#.
///
/// La descripción de la herramienta es la configuración: para sumar una capacidad o una
/// base nueva se escribe su fila, no se recompila.
/// </summary>
public interface ISeleccionHerramientaSemantica
{
    /// <summary>
    /// Devuelve la herramienta cuya descripción se parece más al mensaje, o null si
    /// ninguna supera el umbral o no hay embeddings disponibles.
    /// </summary>
    Task<Herramienta?> SeleccionarAsync(
        string mensaje, IEnumerable<Herramienta> disponibles, double umbral, CancellationToken ct = default);

    /// <summary>Similitud del mensaje con la descripción de una herramienta concreta.</summary>
    Task<double> SimilitudAsync(string mensaje, string descripcion, CancellationToken ct = default);
}

public class SeleccionHerramientaSemantica : ISeleccionHerramientaSemantica
{
    private readonly IEmbeddingProvider? _embeddings;
    private readonly ILoggerLike _log;

    // Caché de embeddings por texto (las descripciones son estables; el mensaje no se cachea).
    private static readonly ConcurrentDictionary<string, float[]> _cache = new(StringComparer.Ordinal);
    private static readonly SemaphoreSlim _lock = new(1, 1);

    public SeleccionHerramientaSemantica(IEmbeddingProvider? embeddings = null, ILoggerLike? log = null)
    {
        _embeddings = embeddings;
        _log = log ?? NullLog.Instance;
    }

    public async Task<Herramienta?> SeleccionarAsync(
        string mensaje, IEnumerable<Herramienta> disponibles, double umbral, CancellationToken ct = default)
    {
        if (_embeddings == null || string.IsNullOrWhiteSpace(mensaje)) return null;

        var candidatas = disponibles
            .Where(h => h.Activa && !string.IsNullOrWhiteSpace(h.Descripcion))
            .ToList();
        if (candidatas.Count == 0) return null;

        try
        {
            var embMensaje = await _embeddings.GenerateEmbeddingAsync(mensaje).WaitAsync(ct);
            Herramienta? mejor = null;
            var mejorSim = double.NegativeInfinity;
            foreach (var h in candidatas)
            {
                var sim = await CosenoAsync(embMensaje, h.Descripcion, ct);
                if (sim > mejorSim) { mejorSim = sim; mejor = h; }
            }

            if (mejor is null || mejorSim < umbral)
            {
                _log.Debug($"SeleccionHerramienta: mejor similitud {mejorSim:F3} < umbral {umbral:F2}; sin herramienta.");
                return null;
            }
            return mejor;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Sin embeddings no se decide por vocabulario: no se dispara ninguna
            // herramienta determinista y el LLM resuelve la pregunta.
            _log.Debug($"SeleccionHerramienta: embeddings no disponibles ({ex.Message}); sin herramienta.");
            return null;
        }
    }

    public async Task<double> SimilitudAsync(string mensaje, string descripcion, CancellationToken ct = default)
    {
        if (_embeddings == null || string.IsNullOrWhiteSpace(mensaje) || string.IsNullOrWhiteSpace(descripcion))
            return 0;
        try
        {
            var a = await _embeddings.GenerateEmbeddingAsync(mensaje).WaitAsync(ct);
            var b = await EmbeddingCacheadoAsync(descripcion, ct);
            return Coseno(a, b);
        }
        catch { return 0; }
    }

    private async Task<double> CosenoAsync(float[] embMensaje, string descripcion, CancellationToken ct)
    {
        var b = await EmbeddingCacheadoAsync(descripcion, ct);
        return Coseno(embMensaje, b);
    }

    private async Task<float[]> EmbeddingCacheadoAsync(string texto, CancellationToken ct)
    {
        if (_cache.TryGetValue(texto, out var e)) return e;
        await _lock.WaitAsync(ct);
        try
        {
            if (_cache.TryGetValue(texto, out e)) return e;
            e = await _embeddings!.GenerateEmbeddingAsync(texto).WaitAsync(ct);
            _cache[texto] = e;
            return e;
        }
        finally { _lock.Release(); }
    }

    public static double Coseno(float[] a, float[] b)
    {
        if (a.Length == 0 || a.Length != b.Length) return 0;
        double dot = 0, na = 0, nb = 0;
        for (int i = 0; i < a.Length; i++)
        {
            dot += (double)a[i] * b[i];
            na += (double)a[i] * a[i];
            nb += (double)b[i] * b[i];
        }
        if (na <= 0 || nb <= 0) return 0;
        return dot / (Math.Sqrt(na) * Math.Sqrt(nb));
    }

    /// <summary>
    /// Margen respecto al mejor fragmento para seguir siendo parte de la respuesta.
    /// </summary>
    public const double MargenRelevanciaDelMejor = 0.12;

    /// <summary>
    /// Un fragmento de otro documento solo sobrevive por puntaje si lo supera.
    /// Calibrado: el relleno entre documentos ajenos llega a 0.64 y los aciertos
    /// reales entre documentos a 0.75-0.81.
    /// </summary>
    public const float SimilitudClaramenteRelevante = 0.70f;

    /// <summary>
    /// Brecha máxima con el mejor para considerar un empate: dos documentos pueden
    /// responder casi igual (plan #9138: 0.671 vs 0.670, brecha 0.001) y ninguno es
    /// ruido; el relleno real queda a 0.07 o más.
    /// </summary>
    public const double BrechaEmpate = 0.03;

    /// <summary>
    /// Recorte de relevancia COMPARTIDO por las vías de recuperación (RagService
    /// global y RecuperacionService con alcance). Decide qué candidatos sobreviven
    /// por PUNTAJES, sin vocabulario:
    ///   · el preferido explícito siempre sobrevive;
    ///   · el resto debe estar a menos de <paramref name="margen"/> del mejor, y
    ///     además ser del documento ganador, claramente relevante o empatado;
    ///   · con <paramref name="puntajeMinimo"/>, el suelo aplica salvo al preferido
    ///     y a los empates de un mejor que sí lo supera (plan #9138).
    /// Devuelve los índices a conservar, en orden.
    /// </summary>
    public static List<int> FiltrarPorRelevancia(
        IReadOnlyList<(string Documento, float Puntaje)> candidatos,
        float? puntajeMinimo = null,
        Func<string, bool>? esPreferido = null,
        double margen = MargenRelevanciaDelMejor,
        float umbralOtraFuente = SimilitudClaramenteRelevante,
        double brechaEmpate = BrechaEmpate)
    {
        var conservados = new List<int>();
        try
        {
            if (candidatos.Count == 0) return conservados;
            var mejor = double.NegativeInfinity;
            string mejorDoc = string.Empty;
            for (var i = 0; i < candidatos.Count; i++)
            {
                if (candidatos[i].Puntaje > mejor)
                {
                    mejor = candidatos[i].Puntaje;
                    mejorDoc = candidatos[i].Documento ?? string.Empty;
                }
            }
            var corte = mejor - margen;
            for (var i = 0; i < candidatos.Count; i++)
            {
                var (doc, puntaje) = candidatos[i];
                if (esPreferido != null)
                {
                    try { if (esPreferido(doc ?? string.Empty)) { conservados.Add(i); continue; } }
                    catch { /* el matcher no puede tumbar el filtro */ }
                }
                if (puntaje < corte) continue;
                if (!DocumentoCoincide(doc, mejorDoc)
                    && puntaje < umbralOtraFuente
                    && mejor - puntaje > brechaEmpate) continue;
                if (puntajeMinimo.HasValue && puntaje < puntajeMinimo.Value
                    && !(mejor >= puntajeMinimo.Value && mejor - puntaje <= brechaEmpate)) continue;
                conservados.Add(i);
            }
        }
        catch { /* ante cualquier duda se devuelve lo ya conservado */ }
        return conservados;
    }

    private static bool DocumentoCoincide(string? a, string? b)
        => !string.IsNullOrWhiteSpace(a) && !string.IsNullOrWhiteSpace(b)
            && string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);
}

/// <summary>Log mínimo para no arrastrar ILogger a este servicio.</summary>
public interface ILoggerLike
{
    void Debug(string mensaje);
}

internal sealed class NullLog : ILoggerLike
{
    public static readonly NullLog Instance = new();
    public void Debug(string mensaje) { }
}
