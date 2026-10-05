using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Asistente.Domain.Entities;
using Asistente.Domain.Interfaces;
using Moq;

namespace Asistente.Tests.Services;

/// <summary>
/// Embeddings deterministas para tests del AgentSelector. El selector enruta por
/// coseno entre la pregunta y la DESCRIPCIÓN de cada herramienta (dato de
/// configuración), así que el test debe poder fijar esa similitud de forma
/// explícita en vez de depender de un modelo real.
/// Ejes: 0 = consultas a base de datos, 1 = documentos, 2 = reportes.
/// </summary>
internal sealed class EmbeddingsPorEje : IEmbeddingProvider
{
    private const int Dimension = 3;

    /// <summary>Asigna un eje (0=datos, 1=documental, 2=reporte) a un texto.</summary>
    private readonly Dictionary<string, int> _ejes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, float[]> _vectores = new(StringComparer.OrdinalIgnoreCase);

    public EmbeddingsPorEje Eje(string texto, int eje)
    {
        _ejes[texto] = eje;
        return this;
    }

    /// <summary>
    /// Vector arbitrario (ya normalizado) para un texto. Una pregunta real que mezcla
    /// varias intenciones tiene similitud media con varias descripciones: con un
    /// embedding de un solo eje el coseno con las demás sería 0 y el test medviría
    /// una geometría que el modelo no produce.
    /// </summary>
    public EmbeddingsPorEje Vector(string texto, float[] vector)
    {
        _vectores[texto] = vector;
        return this;
    }

    public Task<float[]> GenerateEmbeddingAsync(string text)
    {
        if (_vectores.TryGetValue(text, out var propio)) return Task.FromResult(propio);
        var v = new float[Dimension];
        if (_ejes.TryGetValue(text, out var eje) && eje >= 0 && eje < Dimension)
            v[eje] = 1f;
        else
            v[0] = 0.5f; // texto desconocido: vector tenue, no favorece a ninguna capacidad
        return Task.FromResult(v);
    }

    /// <summary>Catálogo de herramientas con descripciones, como los que hay en la BD.</summary>
    public static Mock<IHerramientaRepository> Catalogo(params (string Codigo, string Descripcion, string Categoria)[] herramientas)
    {
        var repo = new Mock<IHerramientaRepository>();
        repo.Setup(r => r.GetActivasAsync()).ReturnsAsync(herramientas
            .Select(h => new Herramienta
            {
                Codigo = h.Codigo,
                Descripcion = h.Descripcion,
                Categoria = h.Categoria,
                Activa = true
            }).ToList());
        return repo;
    }
}
