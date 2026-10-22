using System;
using System.Collections.Generic;
using System.Linq;
using Asistente.Domain.Interfaces;
using Asistente.Infrastructure.Services;
using Xunit;

namespace Asistente.Tests.Services;

/// <summary>
/// Filtro de la busqueda documental en ChromaDB.
///
/// El fallo: SearchWithFilterAsync buscaba SIN filtro (top 30) y descartaba en
/// memoria lo que no era de la fuente pedida. Con 19 documentos y 57 vectores,
/// un documento valido que no caia en el top-30 global desaparecia, y el
/// asistente respondia "no tengo informacion" aunque el fragmento estuviera a
/// uno o dos puestos de la respuesta global.
///
/// El where se arma para que el filtro se aplique en el servidor, y Chroma
/// 0.4.x exige al menos DOS expresiones en $and y $or (con una sola, la
/// consulta se rechaza).
/// </summary>
public class ChromaVectorStoreFiltroTests
{
    [Fact]
    public void SinFiltro_NoHayWhere()
    {
        var where = ChromaVectorStore.ConstruirWhereChroma(new VectorSearchFilter());
        Assert.Null(where);
    }

    [Fact]
    public void UnaSolaFuente_UsaIgualDirecto_PorqueChromaExigeDosExpresionesEnOr()
    {
        var where = ChromaVectorStore.ConstruirWhereChroma(new VectorSearchFilter { IdsFuentes = new List<int> { 2 } });

        Assert.NotNull(where);
        Assert.DoesNotContain("$or", where!.Keys);
        Assert.Contains("id_fuente", where.Keys);
        var condicion = Assert.IsType<Dictionary<string, object>>(where["id_fuente"]);
        Assert.Equal(2, condicion["$eq"]);
    }

    [Fact]
    public void VariasFuentes_UsanOrConAlMenosDosExpresiones()
    {
        var where = ChromaVectorStore.ConstruirWhereChroma(new VectorSearchFilter { IdsFuentes = new List<int> { 1, 2, 3 } });

        Assert.NotNull(where);
        var opciones = Assert.IsType<List<object>>(where!["$or"]);
        Assert.True(opciones.Count >= 2, "Chroma 0.4.x rechaza un $or con una sola expresion.");
    }

    [Fact]
    public void VariasCondiciones_UsanAndConAlMenosDosExpresiones()
    {
        var where = ChromaVectorStore.ConstruirWhereChroma(new VectorSearchFilter
        {
            IdsFuentes = new List<int> { 1, 2 },
            IdCategoria = 3,
            EstadoDocumento = "Activo"
        });

        Assert.NotNull(where);
        var condiciones = Assert.IsType<List<object>>(where!["$and"]);
        Assert.True(condiciones.Count >= 2, "Chroma 0.4.x rechaza un $and con una sola expresion.");
    }

    [Fact]
    public void DocumentosYCategoria_EntranEnElWhere()
    {
        var where = ChromaVectorStore.ConstruirWhereChroma(new VectorSearchFilter
        {
            IdsDocumentos = new List<int> { 1014 },
            IdCategoria = 2
        });

        Assert.NotNull(where);
        var condiciones = Assert.IsType<List<object>>(where!["$and"]);
        var campos = condiciones.Select(c => Assert.IsType<Dictionary<string, object>>(c).Keys.First()).ToList();
        Assert.Contains("id_documento", campos);
        Assert.Contains("id_categoria", campos);
    }
}
