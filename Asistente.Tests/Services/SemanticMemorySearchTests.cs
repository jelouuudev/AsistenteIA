using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Asistente.Application.Services.Memoria;
using Asistente.Domain.Entities;
using Asistente.Domain.Interfaces;
using Asistente.Shared;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Asistente.Tests.Services;

/// <summary>
/// Pruebas de la búsqueda semántica de memorias (ETAPA 21, sección "Semantic
/// Search" del plan de pruebas). Verifican la indexación en el vector store
/// compartido, el marcador que separa la memoria de los documentos RAG y la
/// degradación a búsqueda por texto cuando no hay embeddings.
/// </summary>
public class SemanticMemorySearchTests
{
    private static MemoryRecord Memoria(string codigo, string contenido = "contenido")
        => new MemoryRecord
        {
            Codigo = codigo,
            Titulo = codigo,
            Contenido = contenido,
            IdScope = 2,
            Activo = true,
            VersionActual = 1
        };

    [Fact]
    public async Task IndexarAsync_DejaLaMemoriaIndexadaConMarcadorDeMemoria()
    {
        var vectores = new MemoriaTestDoubles.VectorFalso();
        var busqueda = new SemanticMemorySearch(vectores, new MemoriaTestDoubles.EmbeddingsFalsos(),
            NullLogger<SemanticMemorySearch>.Instance);
        var memoria = Memoria("FORM-REPORTE", "formato del reporte mensual");

        await busqueda.IndexarAsync(memoria);

        Assert.True(memoria.Indexada);
        Assert.NotNull(memoria.VectorId);
        var indexado = Assert.Single(vectores.Indice.Values);
        Assert.Contains("formato del reporte mensual", indexado.Texto);
    }

    [Fact]
    public async Task IndexarAsync_SinEmbeddings_NoRompeYQuedaSinIndexar()
    {
        var vectores = new MemoriaTestDoubles.VectorFalso();
        var busqueda = new SemanticMemorySearch(vectores, new MemoriaTestDoubles.EmbeddingsCaidos(),
            NullLogger<SemanticMemorySearch>.Instance);
        var memoria = Memoria("SIN-EMBEDDING");

        await busqueda.IndexarAsync(memoria);

        Assert.False(memoria.Indexada);
        Assert.Empty(vectores.Indice);
    }

    [Fact]
    public async Task IndexarAsync_CuandoElVectorStoreFalla_DegradaSinTumbarLaOperacion()
    {
        var busqueda = new SemanticMemorySearch(new VectorStoreRoto(), new MemoriaTestDoubles.EmbeddingsFalsos(),
            NullLogger<SemanticMemorySearch>.Instance);
        var memoria = Memoria("VECTOR-ROTO");

        await busqueda.IndexarAsync(memoria);

        Assert.False(memoria.Indexada);
    }

    [Fact]
    public async Task BuscarIdsAsync_SoloDevuelveVectoresMarcadosComoMemoria()
    {
        var vectores = new MemoriaTestDoubles.VectorFalso();
        var busqueda = new SemanticMemorySearch(vectores, new MemoriaTestDoubles.EmbeddingsFalsos(),
            NullLogger<SemanticMemorySearch>.Instance);
        var memoria = Memoria("BUSCABLE", "formato de reporte mensual institucional");
        await busqueda.IndexarAsync(memoria);

        // Un vector de documento (no de memoria) no debe entrar en el resultado.
        await vectores.IndexAsync(new VectorDocument
        {
            DocumentId = Guid.NewGuid(),
            Text = "formato de reporte mensual de un documento pdf",
            Embedding = new float[] { 1f, 0f, 0f, 0f },
            MetadataDocumentoNombre = "documento.pdf"
        });

        var hits = await busqueda.BuscarIdsAsync("formato de reporte mensual", 5);

        Assert.Single(hits);
        Assert.Equal(memoria.VectorId!.Value, hits[0].VectorId);
    }

    [Fact]
    public async Task DesindexarAsync_BorraElVectorYMarcaLaMemoriaComoNoIndexada()
    {
        var vectores = new MemoriaTestDoubles.VectorFalso();
        var busqueda = new SemanticMemorySearch(vectores, new MemoriaTestDoubles.EmbeddingsFalsos(),
            NullLogger<SemanticMemorySearch>.Instance);
        var memoria = Memoria("A_BORRAR");
        await busqueda.IndexarAsync(memoria);
        Assert.Single(vectores.Indice);

        await busqueda.DesindexarAsync(memoria);

        Assert.False(memoria.Indexada);
        Assert.Null(memoria.VectorId);
        Assert.Empty(vectores.Indice);
    }

    [Fact]
    public async Task BuscarIdsAsync_CuandoElVectorStoreFalla_DevuelveVacioSinTumbar()
    {
        var busqueda = new SemanticMemorySearch(new VectorStoreRoto(), new MemoriaTestDoubles.EmbeddingsFalsos(),
            NullLogger<SemanticMemorySearch>.Instance);

        var hits = await busqueda.BuscarIdsAsync("reporte", 5);

        Assert.Empty(hits);
    }

    private sealed class VectorStoreRoto : IVectorStore
    {
        public Task IndexAsync(VectorDocument d) => throw new InvalidOperationException("caido");
        public Task IndexBatchAsync(IEnumerable<VectorDocument> ds) => throw new InvalidOperationException("caido");
        public Task<IEnumerable<VectorSearchResult>> SearchAsync(string query, int topK) => throw new InvalidOperationException("caido");
        public Task<IEnumerable<VectorSearchResult>> SearchWithFilterAsync(string q, int k, VectorSearchFilter f) => throw new InvalidOperationException("caido");
        public Task<IEnumerable<VectorSearchResult>> SearchByDocumentAsync(string q, int k, string n) => throw new InvalidOperationException("caido");
        public Task<IEnumerable<string>> GetAllDocumentNamesAsync() => throw new InvalidOperationException("caido");
        public Task<IEnumerable<VectorSearchResult>> GetByDocumentoProcesadoIdAsync(int id) => throw new InvalidOperationException("caido");
        public Task<IEnumerable<int>> GetAllDocumentoProcesadoIdsAsync() => throw new InvalidOperationException("caido");
        public Task DeleteDocumentAsync(Guid id) => throw new InvalidOperationException("caido");
        public Task DeleteByDocumentoProcesadoIdAsync(int id) => throw new InvalidOperationException("caido");
        public Task<int> GetDocumentCountAsync() => throw new InvalidOperationException("caido");
        public Task<bool> HealthCheckAsync() => throw new InvalidOperationException("caido");
        public Task<string> GetDocumentTextAsync(int id) => throw new InvalidOperationException("caido");
        public Task ClearAsync() => throw new InvalidOperationException("caido");
        public Task<IEnumerable<(string Nombre, int ChunkCount)>> GetDocumentCountsAsync() => throw new InvalidOperationException("caido");
    }
}
