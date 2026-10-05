using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Asistente.Application.Orchestrator.Planner;
using Asistente.Domain.Entities;
using Asistente.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Asistente.Tests.Services;

/// <summary>
/// Rescate RAG semántico: el LLM clasificador no ve el contenido de los
/// documentos, así que una pregunta servida por un archivo recién subido (cuyo
/// nombre no dice nada, p.ej. "a") llegaba sin ruta RAG. El Planner compara el
/// objetivo con la firma real de cada documento indexado y, si gana sobre el
/// esquema, activa RAG.
/// </summary>
public class PlanBuilderRagSemanticoTests
{
    private sealed class EmbPorPunto : IEmbeddingProvider
    {
        // Vectores ortogonales: el coseno vale 1 con el "punto" del objetivo y ~0 con
        // cualquier otro texto. Permite decidir por punteros, sin vocabulario.
        private readonly Dictionary<string, float[]> _mapa;
        private readonly string[] _palabras;
        public EmbPorPunto(string[] palabras) { _palabras = palabras; _mapa = new(); }
        public void Definir(string texto, int indice)
        {
            var v = new float[_palabras.Length];
            v[indice] = 1f;
            _mapa[texto] = v;
        }
        public Task<float[]> GenerateEmbeddingAsync(string text)
        {
            foreach (var (clave, vec) in _mapa)
                if (text.Contains(clave, StringComparison.OrdinalIgnoreCase))
                    return Task.FromResult(vec);
            var otro = new float[_palabras.Length];
            otro[_palabras.Length - 1] = 1f;
            return Task.FromResult(otro);
        }
    }

    private static Asistente.Domain.Entities.Asistente Agente() => new()
    {
        IdAsistente = 1,
        Codigo = "PRINCIPAL",
        Nombre = "Principal",
        Activo = true
    };

    private static Mock<IAsistenteRepository> RepoAsistentes()
    {
        var repo = new Mock<IAsistenteRepository>();
        var agente = Agente();
        repo.Setup(r => r.GetAllAsync()).ReturnsAsync(new[] { agente });
        repo.Setup(r => r.GetByIdAsync(It.IsAny<int>())).ReturnsAsync(agente);
        return repo;
    }

    private static Mock<IConexionBaseDatosRepository> RepoConexion()
    {
        var repo = new Mock<IConexionBaseDatosRepository>();
        repo.Setup(r => r.GetActivasAsync()).ReturnsAsync(new[]
        {
            new ConexionBaseDatos
            {
                IdConexion = 1,
                Nombre = "ControlActivosTest",
                BaseDatos = "ControlActivosTest",
                Activa = true,
                CadenaConexionCifrada = "x",
                TablasAutorizadas = new List<TablaAutorizada> { new() { NombreTabla = "Activos" } }
            }
        });
        return repo;
    }

    private static Mock<IConexionCifrador> Cifrador()
    {
        var mock = new Mock<IConexionCifrador>();
        mock.Setup(c => c.Descifrar(It.IsAny<string>())).Returns("Server=x");
        return mock;
    }

    private static Mock<ISqlQueryExecutor> Executor()
    {
        var mock = new Mock<ISqlQueryExecutor>();
        mock.Setup(e => e.ExecuteReadOnlyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<int>(), It.IsAny<CancellationToken>(), It.IsAny<int>()))
            .ReturnsAsync((string _c, string sql, object? _p, int _n, CancellationToken _t, int _x) =>
                sql.Contains("DATA_TYPE IN")
                    ? new List<Dictionary<string, object?>> { new() { ["COLUMN_NAME"] = "Categoria" } }
                    : sql.Contains("INFORMATION_SCHEMA.COLUMNS")
                        ? new List<Dictionary<string, object?>>
                        {
                            new() { ["COLUMN_NAME"] = "IdActivo" }, new() { ["COLUMN_NAME"] = "Nombre" },
                            new() { ["COLUMN_NAME"] = "Categoria" }, new() { ["COLUMN_NAME"] = "Precio" }
                        }
                        : new List<Dictionary<string, object?>>());
        return mock;
    }

    private static Mock<IOllamaService> OllamaSinRag()
    {
        var mock = new Mock<IOllamaService>();
        mock.Setup(o => o.SendMessageAsync(It.IsAny<IEnumerable<Mensaje>>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<double?>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("{\"tablas\":[],\"rag\":false,\"reporte\":false,\"riesgo\":false,\"workflow\":false,\"agregacion\":false,\"aprobacion\":false,\"subconsultas\":[]}");
        mock.Setup(o => o.IsDisponibleAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        return mock;
    }

    private static Mock<IDocumentoRepository> RepoDocumentos(params DocumentoContenidoResumen[] contenidos)
    {
        var repo = new Mock<IDocumentoRepository>();
        repo.Setup(r => r.GetAllAsync()).ReturnsAsync(Array.Empty<Documento>());
        repo.Setup(r => r.GetContenidosIndexadosAsync(It.IsAny<int>())).ReturnsAsync(contenidos);
        return repo;
    }

    private static PlanBuilder Crear(EmbPorPunto emb, Mock<IDocumentoRepository> docs) => new(
        RepoAsistentes().Object,
        OllamaSinRag().Object,
        new Mock<ILogger<PlanBuilder>>().Object,
        RepoConexion().Object,
        new Mock<IWorkflowRepository>().Object,
        emb,
        Cifrador().Object,
        Executor().Object,
        docs.Object);

    [Fact]
    public async Task DocumentoIndexado_QueRespondeElObjetivo_ActivaRag()
    {
        // El objetivo apunta al contenido del manual (punto 0); el manual lo contiene.
        var emb = new EmbPorPunto(new[] { "sistema solar", "activos" });
        emb.Definir("planetas del sistema solar", 0);
        emb.Definir("IdActivo Nombre Categoria Precio", 1);

        var docs = RepoDocumentos(new DocumentoContenidoResumen
        {
            IdDocumento = 3,
            Codigo = "a",
            Nombre = "a",
            Texto = "Planetas del Sistema Solar: distancia Tierra a Marte 78 millones de km",
            TotalChunks = 1
        });

        var plan = await Crear(emb, docs).ConstruirAsync(
            "dime cuales son los Planetas del Sistema Solar", 1, CancellationToken.None);

        Assert.Contains(plan.Pasos, p => p.CodigoHerramienta == "DocumentSearchTool");
    }

    [Fact]
    public async Task DocumentoNoRelacionado_NoActivaRag()
    {
        // Objetivo en el punto de esquema (Activos), documento en otro punto.
        var emb = new EmbPorPunto(new[] { "sistema solar", "activos" });
        emb.Definir("cuantos activos hay con precio mayor a 1000", 1);
        emb.Definir("planetas del sistema solar", 0);

        var docs = RepoDocumentos(new DocumentoContenidoResumen
        {
            IdDocumento = 3,
            Codigo = "a",
            Nombre = "a",
            Texto = "Planetas del Sistema Solar y distancias en millones de km",
            TotalChunks = 1
        });

        var plan = await Crear(emb, docs).ConstruirAsync(
            "cuantos activos hay con precio mayor a 1000", 1, CancellationToken.None);

        Assert.DoesNotContain(plan.Pasos, p => p.CodigoHerramienta == "DocumentSearchTool");
    }

    [Fact]
    public async Task SinDocumentosIndexados_NoActivaRag()
    {
        var emb = new EmbPorPunto(new[] { "sistema solar", "activos" });
        emb.Definir("planetas del sistema solar", 0);

        var plan = await Crear(emb, RepoDocumentos()).ConstruirAsync(
            "dime cuales son los Planetas del Sistema Solar", 1, CancellationToken.None);

        Assert.DoesNotContain(plan.Pasos, p => p.CodigoHerramienta == "DocumentSearchTool");
    }
}