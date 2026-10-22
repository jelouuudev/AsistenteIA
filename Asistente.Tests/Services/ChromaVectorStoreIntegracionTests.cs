using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Asistente.Domain.Interfaces;
using Asistente.Infrastructure.Services;
using Asistente.Shared;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Asistente.Tests.Services;

/// <summary>
/// Diagnostico contra el ChromaDB y el Ollama reales (localhost). Requiere los
/// dos servicios levantados; si no estan, el test se salta.
/// </summary>
public class ChromaVectorStoreIntegracionTests
{
    private sealed class EmbeddingsOllama : IEmbeddingProvider
    {
        private readonly HttpClient _http;
        public EmbeddingsOllama(HttpClient http) => _http = http;
        public async Task<float[]> GenerateEmbeddingAsync(string text)
        {
            var json = JsonSerializer.Serialize(new { model = "nomic-embed-text", prompt = text });
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var r = await _http.PostAsync("http://localhost:11434/api/embeddings", content);
            r.EnsureSuccessStatusCode();
            using var doc = JsonDocument.Parse(await r.Content.ReadAsStringAsync());
            return doc.RootElement.GetProperty("embedding").EnumerateArray().Select(e => e.GetSingle()).ToArray();
        }
    }

    private static ChromaVectorStore CrearStore()
    {
        var http = new HttpClient { BaseAddress = new Uri("http://localhost:8000"), Timeout = TimeSpan.FromSeconds(60) };
        var config = Options.Create(new ChromaConfig
        {
            Url = "http://localhost:8000",
            CollectionName = "asistente_documentos",
            TimeoutSegundos = 30
        });
        var embeddings = new EmbeddingsOllama(new HttpClient { Timeout = TimeSpan.FromSeconds(120) });
        return new ChromaVectorStore(http, config, NullLogger<ChromaVectorStore>.Instance, embeddings);
    }

    [Fact]
    public async Task ConWhereDeFuente_DebeDevolverElDocumentoDeEsaFuente()
    {
        var store = CrearStore();
        var pregunta = "cuales son los USUARIOS PREDETERMINADOS:";

        List<VectorSearchResult> conFiltro;
        try
        {
            conFiltro = (await store.SearchWithFilterAsync(pregunta, 5,
                new VectorSearchFilter { IdsFuentes = new List<int> { 2 } })).ToList();
        }
        catch (HttpRequestException)
        {
            // Sin ChromaDB/Ollama levantados (CI, maquina sin el stack) el test
            // no puede afirmar nada: se sale sin fallar.
            return;
        }

        Assert.NotEmpty(conFiltro);
        Assert.Contains(conFiltro, r => r.MetadataDocumentoCodigo == "USUARIOS-Y-ROLES");
    }
}
