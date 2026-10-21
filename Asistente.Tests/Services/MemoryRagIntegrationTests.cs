using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Asistente.Application.Interfaces;
using Asistente.Application.Services;
using Asistente.Application.Services.Herramientas;
using Asistente.Application.Services.Memoria;
using Asistente.Domain.Entities;
using Asistente.Domain.Interfaces;
using Asistente.Shared;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Asistente.Tests.Services;

/// <summary>
/// Integración memoria ↔ RAG (ETAPA 21, sección 11 de la especificación:
/// "la memoria deberá integrarse con ... RAG"). Verifica que el contexto RAG
/// lleva el bloque de memoria institucional y que la herramienta de búsqueda
/// documental de los agentes lo entrega junto al contexto documental.
/// </summary>
public class MemoryRagIntegrationTests
{
    private static async Task<(MemoryManager Manager, MemoriaTestDoubles.RepoMemoria Repo,
        MemoriaTestDoubles.RepoLogs Logs, MemoryOrchestrator Orchestrator, VectorStoreVacio Vectores)> PrepararAsync(bool conMemoria = true)
    {
        var (manager, repo, _, logs, _, _) = MemoriaTestDoubles.CrearManager(MemoriaTestDoubles.Auth());
        var vectores = new VectorStoreVacio();
        if (conMemoria)
        {
            await manager.CrearAsync(new CrearMemoryRequest
            {
                Codigo = "FORM-REPORTE-MENSUAL",
                Titulo = "Formato de reporte mensual",
                Contenido = "Formato oficial del reporte mensual: resumen, indicadores, incidencias.",
                Ambito = "Organizacion",
                Categoria = "FormatoReporte",
                RequierePermiso = false
            }, 1);
        }
        var orchestrator = MemoriaTestDoubles.CrearOrchestrator(manager, repo);
        return (manager, repo, logs, orchestrator, vectores);
    }

    private static RagService CrearRag(MemoryOrchestrator orchestrator, IVectorStore vectores)
    {
        var configRepo = new Mock<IEmbeddingConfiguracionRepository>();
        configRepo.Setup(r => r.GetActivaAsync()).ReturnsAsync((EmbeddingConfiguracion?)null);
        return new RagService(vectores, new MemoriaTestDoubles.EmbeddingsFalsos(), configRepo.Object,
            NullLogger<RagService>.Instance, null, orchestrator);
    }

    [Fact]
    public async Task RecuperarContextoDocumental_ConUsuario_TraeElBloqueDeMemoriaInstitucional()
    {
        var (manager, repo, logs, orchestrator, vectores) = await PrepararAsync();
        var rag = CrearRag(orchestrator, vectores);

        var contexto = await rag.RecuperarContextoDocumentalAsync("reporte mensual", 5, null, 1, null);

        Assert.Contains("Formato de reporte mensual", contexto.ContextoMemoria);
        Assert.Contains("Organizacion", contexto.ContextoMemoria);
    }

    [Fact]
    public async Task RecuperarContextoDocumental_SinUsuario_NoInyectaMemoria()
    {
        var (manager, repo, logs, orchestrator, vectores) = await PrepararAsync();
        var rag = CrearRag(orchestrator, vectores);

        var contexto = await rag.RecuperarContextoDocumentalAsync("reporte mensual", 5, null, null, null);

        Assert.Empty(contexto.ContextoMemoria);
    }

    [Fact]
    public async Task RecuperarContextoDocumental_LaMemoriaSeAuditaComoInyectada()
    {
        var (manager, repo, logs, orchestrator, vectores) = await PrepararAsync();
        var rag = CrearRag(orchestrator, vectores);

        await rag.RecuperarContextoDocumentalAsync("reporte mensual", 5, null, 1, null);

        Assert.Contains(logs.Datos, l => l.Codigo == "FORM-REPORTE-MENSUAL" && l.Operacion == "Inyectar");
    }

    [Fact]
    public async Task DocumentSearchTool_EntregaLaMemoriaJuntoAlContextoDocumental()
    {
        var (manager, repo, logs, orchestrator, vectores) = await PrepararAsync();
        var rag = CrearRag(orchestrator, vectores);
        var herramienta = new DocumentSearchTool(rag, NullLogger<DocumentSearchTool>.Instance, null, orchestrator);

        var resultado = await herramienta.ExecuteAsync(new ToolExecutionRequest
        {
            HerramientaCodigo = "DocumentSearchTool",
            IdUsuario = 1,
            IdAsistente = 1,
            Parametros = new Dictionary<string, object?> { ["consulta"] = "formato de reporte mensual" }
        });

        Assert.True(resultado.Exitoso);
        Assert.Contains("Formato de reporte mensual", resultado.Contenido ?? string.Empty);
        Assert.DoesNotContain("[SIN_DATOS]", resultado.Contenido ?? string.Empty);
    }

    [Fact]
    public async Task DocumentSearchTool_SinMemoria_SigueDevolviendoElContextoDocumental()
    {
        var (manager, repo, logs, orchestrator, vectores) = await PrepararAsync(conMemoria: false);
        var rag = CrearRag(orchestrator, vectores);
        var herramienta = new DocumentSearchTool(rag, NullLogger<DocumentSearchTool>.Instance, null, orchestrator);

        var resultado = await herramienta.ExecuteAsync(new ToolExecutionRequest
        {
            HerramientaCodigo = "DocumentSearchTool",
            IdUsuario = 1,
            IdAsistente = 1,
            Parametros = new Dictionary<string, object?> { ["consulta"] = "formato de reporte mensual" }
        });

        Assert.True(resultado.Exitoso);
        Assert.DoesNotContain("FORM-REPORTE-MENSUAL", resultado.Contenido ?? string.Empty);
    }

    /// <summary>
    /// Saca el repositorio de logs del manager por reflexión: el manager no lo
    /// expone y aqui solo se necesita comprobar la auditoria de inyeccion.
    /// </summary>
    private static object ObtenerLogs(MemoryManager manager)
    {
        var campo = typeof(MemoryManager).GetField("_logs",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(campo);
        return campo!.GetValue(manager)!;
    }

    /// <summary>Vector store sin documentos: el RAG no aporta fragmentos, solo la memoria.</summary>
    private sealed class VectorStoreVacio : IVectorStore
    {
        public Task IndexAsync(VectorDocument d) => Task.CompletedTask;
        public Task IndexBatchAsync(IEnumerable<VectorDocument> ds) => Task.CompletedTask;
        public Task<IEnumerable<VectorSearchResult>> SearchAsync(string query, int topK)
            => Task.FromResult(Enumerable.Empty<VectorSearchResult>());
        public Task<IEnumerable<VectorSearchResult>> SearchWithFilterAsync(string q, int k, VectorSearchFilter f)
            => Task.FromResult(Enumerable.Empty<VectorSearchResult>());
        public Task<IEnumerable<VectorSearchResult>> SearchByDocumentAsync(string q, int k, string n)
            => Task.FromResult(Enumerable.Empty<VectorSearchResult>());
        public Task<IEnumerable<string>> GetAllDocumentNamesAsync()
            => Task.FromResult(Enumerable.Empty<string>());
        public Task<IEnumerable<VectorSearchResult>> GetByDocumentoProcesadoIdAsync(int id)
            => Task.FromResult(Enumerable.Empty<VectorSearchResult>());
        public Task<IEnumerable<int>> GetAllDocumentoProcesadoIdsAsync()
            => Task.FromResult(Enumerable.Empty<int>());
        public Task DeleteDocumentAsync(Guid id) => Task.CompletedTask;
        public Task DeleteByDocumentoProcesadoIdAsync(int id) => Task.CompletedTask;
        public Task<int> GetDocumentCountAsync() => Task.FromResult(0);
        public Task<bool> HealthCheckAsync() => Task.FromResult(true);
        public Task<string> GetDocumentTextAsync(int id) => Task.FromResult(string.Empty);
        public Task ClearAsync() => Task.CompletedTask;
        public Task<IEnumerable<(string Nombre, int ChunkCount)>> GetDocumentCountsAsync()
            => Task.FromResult(Enumerable.Empty<(string, int)>());
    }
}
