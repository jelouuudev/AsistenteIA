using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Asistente.Domain.Entities;
using Asistente.Domain.Interfaces;
using Asistente.Infrastructure.Services;
using Moq;
using Xunit;

namespace Asistente.Tests;

/// <summary>
/// El almacén vectorial comparte colección entre documentos y memorias, así que
/// la frontera entre ambos es un requisito de seguridad, no de organization:
/// el RAG autoriza por documento y nunca aplica el permiso "MEMORIA:{codigo}"
/// que sí exige el Memory Manager.
///
/// Estos tests fijan la regla en MemorySafeVectorStore, que es donde debe vivir:
/// si el filtro estuviera en cada servicio, bastaría con que apareciera uno nuevo
/// para reabrir el hueco, y nada lo detectaría.
/// </summary>
public class MemorySafeVectorStoreTests
{
    private static VectorSearchResult Memoria(string codigo, float score = 0.95f) => new()
    {
        Text = $"contenido privado de {codigo}",
        Score = score,
        MetadataDocumentoNombre = $"MEMORIA:{codigo}"
    };

    private static VectorSearchResult Documento(string nombre, float score = 0.8f) => new()
    {
        Text = $"contenido de {nombre}",
        Score = score,
        MetadataDocumentoNombre = nombre
    };

    private static (MemorySafeVectorStore Seguro, Mock<IVectorStore> Crudo) Construir(
        params VectorSearchResult[] resultados)
    {
        var crudo = new Mock<IVectorStore>();
        var lista = resultados.ToList();
        crudo.Setup(c => c.SearchAsync(It.IsAny<string>(), It.IsAny<int>()))
            .ReturnsAsync(lista);
        crudo.Setup(c => c.SearchWithFilterAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<VectorSearchFilter>()))
            .ReturnsAsync(lista);
        crudo.Setup(c => c.SearchByDocumentAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>()))
            .ReturnsAsync(lista);
        crudo.Setup(c => c.GetByDocumentoProcesadoIdAsync(It.IsAny<int>()))
            .ReturnsAsync(lista);
        crudo.Setup(c => c.GetAllDocumentNamesAsync())
            .ReturnsAsync(lista.Select(r => r.MetadataDocumentoNombre!).ToList());
        crudo.Setup(c => c.GetDocumentCountsAsync())
            .ReturnsAsync(lista
                .GroupBy(r => r.MetadataDocumentoNombre!)
                .Select(g => (g.Key, g.Count())));
        return (new MemorySafeVectorStore(crudo.Object), crudo);
    }

    [Theory]
    [InlineData("SearchAsync")]
    [InlineData("SearchWithFilterAsync")]
    [InlineData("SearchByDocumentAsync")]
    [InlineData("GetByDocumentoProcesadoIdAsync")]
    public async Task Ninguna_Lectura_Devuelve_Vectores_De_Memoria(string metodo)
    {
        var (seguro, _) = Construir(
            Memoria("SECRETA", 0.99f),
            Documento("Manual de Producto"));

        IEnumerable<VectorSearchResult> resultado = metodo switch
        {
            "SearchAsync" => await seguro.SearchAsync("q", 5),
            "SearchWithFilterAsync" => await seguro.SearchWithFilterAsync("q", 5, new VectorSearchFilter()),
            "SearchByDocumentAsync" => await seguro.SearchByDocumentAsync("q", 5, "Manual"),
            _ => await seguro.GetByDocumentoProcesadoIdAsync(1)
        };

        var lista = resultado.ToList();
        Assert.Single(lista);
        Assert.Equal("Manual de Producto", lista[0].MetadataDocumentoNombre);
        Assert.DoesNotContain(lista, r => r.MetadataDocumentoNombre!.StartsWith("MEMORIA:"));
    }

    /// <summary>
    /// El caso dangerous: la memoria con MEJOR score gana. Si el filtro actuara
    /// despues de recortar por score, se llevaria el chunk equivocado.
    /// </summary>
    [Fact]
    public async Task Memoria_Con_Mejor_Score_No_Desplaza_Al_Documento_Valido()
    {
        var (seguro, _) = Construir(
            Memoria("PRIVADA", 0.99f),
            Documento("Manual", 0.30f));

        var lista = (await seguro.SearchAsync("q", 5)).ToList();

        Assert.Single(lista);
        Assert.Equal("Manual", lista[0].MetadataDocumentoNombre);
    }

    [Fact]
    public async Task Listado_De_Documentos_No_Expone_Memorias()
    {
        var (seguro, _) = Construir(Memoria("SECRETA"), Documento("Manual"));

        var nombres = (await seguro.GetAllDocumentNamesAsync()).ToList();
        var conteos = (await seguro.GetDocumentCountsAsync()).ToList();

        Assert.Equal(new[] { "Manual" }, nombres);
        Assert.Equal("Manual", conteos.Single().Nombre);
    }

    /// <summary>Sin memorias, el filtro no debe alterar nada.</summary>
    [Fact]
    public async Task Sin_Memorias_No_Altera_Los_Resultados()
    {
        var (seguro, _) = Construir(Documento("A"), Documento("B"));

        var lista = (await seguro.SearchAsync("q", 5)).ToList();

        Assert.Equal(2, lista.Count);
    }

    /// <summary>Las escrituras deben pasar intactas: indexar es legítimo.</summary>
    [Fact]
    public async Task Escrituras_Pasan_Intactas()
    {
        var (seguro, crudo) = Construir();

        await seguro.IndexAsync(new VectorDocument { DocumentId = Guid.NewGuid(), Text = "x" });
        await seguro.DeleteDocumentAsync(Guid.NewGuid());

        crudo.Verify(c => c.IndexAsync(It.IsAny<VectorDocument>()), Times.Once);
        crudo.Verify(c => c.DeleteDocumentAsync(It.IsAny<Guid>()), Times.Once);
    }

    [Fact]
    public void Prefijo_Reconocido_Independientemente_De_Mayusculas()
    {
        Assert.True(MemorySafeVectorStore.EsVectorDeMemoria(
            new VectorSearchResult { MetadataDocumentoNombre = "memoria:x" }));
        Assert.False(MemorySafeVectorStore.EsVectorDeMemoria(
            new VectorSearchResult { MetadataDocumentoNombre = "Manual" }));
        Assert.False(MemorySafeVectorStore.EsVectorDeMemoria(new VectorSearchResult()));
    }
}