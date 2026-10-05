using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Asistente.Application.Orchestrator.Planner;
using Asistente.Domain.Entities;
using Asistente.Domain.Enums;
using Asistente.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Asistente.Tests.Services;

/// <summary>
/// Puerta fuera-de-dominio del Planner (plan #9075): el LLM nombró [Activos]
/// para "Planetas del Sistema Solar". Con puntaje de esquema 0, similitud bajo
/// umbral y ningún valor observado, las tablas se descartan y el plan queda de
/// conocimiento general (el agente responde directo) en vez de consultar SQL
/// irrelevante. Las paráfrasis legítimas se salvan por cada señal.
/// </summary>
public class PlanBuilderFueraDeDominioTests
{
    private sealed class EmbFijo : IEmbeddingProvider
    {
        private readonly float[] _pregunta;
        private readonly float[] _tablas;
        public EmbFijo(float[] pregunta, float[] tablas) { _pregunta = pregunta; _tablas = tablas; }
        public Task<float[]> GenerateEmbeddingAsync(string text)
            => Task.FromResult(text.Contains("IdActivo") ? _tablas : _pregunta);
    }

    private static Asistente.Domain.Entities.Asistente Agente(int id) => new()
    {
        IdAsistente = id,
        Codigo = "AG",
        Nombre = "AG",
        Activo = true
    };

    private static Mock<IAsistenteRepository> RepoAsistentes()
    {
        var repo = new Mock<IAsistenteRepository>();
        var agentes = new List<Asistente.Domain.Entities.Asistente> { Agente(1008), Agente(2005) };
        repo.Setup(r => r.GetAllAsync()).ReturnsAsync(agentes);
        repo.Setup(r => r.GetByIdAsync(It.IsAny<int>()))
            .ReturnsAsync((int id) => agentes.FirstOrDefault(a => a.IdAsistente == id));
        return repo;
    }

    private static Mock<IConexionBaseDatosRepository> RepoConexion()
    {
        var repo = new Mock<IConexionBaseDatosRepository>();
        var con = new ConexionBaseDatos
        {
            IdConexion = 9,
            Nombre = "ControlActivosTest",
            BaseDatos = "ControlActivosTest",
            Activa = true,
            CadenaConexionCifrada = "x",
            TablasAutorizadas = new List<TablaAutorizada> { new() { NombreTabla = "Activos" } }
        };
        repo.Setup(r => r.GetActivasAsync()).ReturnsAsync(new[] { con });
        return repo;
    }

    private static Mock<IWorkflowRepository> RepoWorkflow()
    {
        var repo = new Mock<IWorkflowRepository>();
        repo.Setup(r => r.GetActivosAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Workflow>());
        return repo;
    }

    private static Mock<IOllamaService> OllamaTablasActivos()
    {
        var mock = new Mock<IOllamaService>();
        mock.Setup(o => o.SendMessageAsync(It.IsAny<IEnumerable<Mensaje>>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<double?>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("{\"tablas\":[\"Activos\"],\"rag\":false,\"reporte\":false,\"riesgo\":false,\"workflow\":false,\"agregacion\":false,\"aprobacion\":false,\"subconsultas\":[]}");
        mock.Setup(o => o.IsDisponibleAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        return mock;
    }

    private static Dictionary<string, object?> Fila(string col, string val)
        => new() { [col] = val };

    private static Mock<ISqlQueryExecutor> ExecutorValores()
    {
        var mock = new Mock<ISqlQueryExecutor>();
        mock.Setup(e => e.ExecuteReadOnlyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<int>(), It.IsAny<CancellationToken>(), It.IsAny<int>()))
            .ReturnsAsync((string _cs, string sql, object? _p, int _n, CancellationToken _ct, int _t) =>
            {
                if (sql.Contains("SELECT DISTINCT"))
                {
                    if (sql.Contains("[Estado]"))
                        return new List<Dictionary<string, object?>> { Fila("V", "ACTIVO"), Fila("V", "INACTIVO") };
                    if (sql.Contains("[Categoria]"))
                        return new List<Dictionary<string, object?>> { Fila("V", "Mobiliario"), Fila("V", "Vehículos") };
                    return new List<Dictionary<string, object?>>();
                }
                if (sql.Contains("DATA_TYPE IN"))
                    return new List<Dictionary<string, object?>>
                    {
                        Fila("COLUMN_NAME", "Nombre"), Fila("COLUMN_NAME", "Categoria"), Fila("COLUMN_NAME", "Estado")
                    };
                if (sql.Contains("INFORMATION_SCHEMA.COLUMNS"))
                    return new List<Dictionary<string, object?>>
                    {
                        Fila("COLUMN_NAME", "IdActivo"), Fila("COLUMN_NAME", "Nombre"),
                        Fila("COLUMN_NAME", "Categoria"), Fila("COLUMN_NAME", "Estado"),
                        Fila("COLUMN_NAME", "Precio")
                    };
                return new List<Dictionary<string, object?>>();
            });
        return mock;
    }

    private static Mock<IConexionCifrador> Cifrador()
    {
        var mock = new Mock<IConexionCifrador>();
        mock.Setup(c => c.Descifrar(It.IsAny<string>())).Returns("Server=x");
        return mock;
    }

    private static PlanBuilder CrearBuilder(IEmbeddingProvider emb, Mock<IDocumentoRepository>? documentos = null) => new(
        RepoAsistentes().Object,
        OllamaTablasActivos().Object,
        new Mock<ILogger<PlanBuilder>>().Object,
        RepoConexion().Object,
        RepoWorkflow().Object,
        emb,
        Cifrador().Object,
        ExecutorValores().Object,
        documentos?.Object);

    /// <summary>
    /// Reparte el embedding por el TEXTO que recibe: la firma del documento, la
    /// firma de la tabla o la pregunta. Permite fijar a mano los tres puntajes con
    /// que se mide la partida documento↔esquema.
    /// </summary>
    private sealed class EmbeddingsPorFirma : IEmbeddingProvider
    {
        private readonly float[] _pregunta, _tabla, _documento;
        public EmbeddingsPorFirma(float cosTabla, float cosDocumento)
        {
            _pregunta = new[] { 1f, 0f };
            _tabla = new[] { cosTabla, (float)Math.Sqrt(1 - cosTabla * cosTabla) };
            _documento = new[] { cosDocumento, (float)Math.Sqrt(1 - cosDocumento * cosDocumento) };
        }
        public Task<float[]> GenerateEmbeddingAsync(string text)
        {
            if (text.Contains("IdActivo")) return Task.FromResult(_tabla);
            if (text.Contains("manual astronomico")) return Task.FromResult(_documento);
            return Task.FromResult(_pregunta);
        }
    }

    private static Mock<IDocumentoRepository> RepoDocumentoAstronomia() => new();

    private static Mock<IDocumentoRepository> RepoDocumentoSostenibilidad()
    {
        var repo = new Mock<IDocumentoRepository>();
        repo.Setup(r => r.GetAllAsync()).ReturnsAsync(new[]
        {
            new Documento { IdDocumento = 6, Codigo = "s", Nombre = "s", Estado = EstadoDocumento.Activo }
        });
        repo.Setup(r => r.GetNombresArchivoAsync())
            .ReturnsAsync(new[] { (6, "sostenibilidad_v1.pdf") });
        repo.Setup(r => r.GetContenidosIndexadosAsync(It.IsAny<int>()))
            .ReturnsAsync(Array.Empty<DocumentoContenidoResumen>());
        return repo;
    }

    [Fact]
    public async Task TypoEnNombreDeArchivo_RescataRag_ConPreferencia()
    {
        // Plan #10170: "segun sotenibilidad dime las iniciativas actuales". El LLM
        // dice rag=false y la sonda semántica queda muda (sin contenidos indexados
        // en el mock); el rescate tolerante a typos empareja "sotenibilidad" con
        // el archivo "sostenibilidad_v1.pdf" y el paso RAG lleva la preferencia.
        var builder = CrearBuilder(
            new EmbFijo(new[] { 1f, 0f }, new[] { 0.5f, 0.866f }),
            RepoDocumentoSostenibilidad());

        var plan = await builder.ConstruirAsync(
            "segun sotenibilidad dime las iniciativas actuales", 1, CancellationToken.None);

        var rag = plan.Pasos.SingleOrDefault(p => p.CodigoHerramienta == "DocumentSearchTool");
        Assert.NotNull(rag);
        Assert.NotNull(rag.Entrada);
        using var doc = System.Text.Json.JsonDocument.Parse(rag.Entrada);
        Assert.Equal("s", doc.RootElement.GetProperty("documento").GetString());
    }

    [Fact]
    public async Task PreguntaAjena_DescartaTablas_PlanSinSql()
    {
        // Similitud 0.5 < 0.55, sin esquema ni valores: fuera de dominio.
        var builder = CrearBuilder(new EmbFijo(new[] { 1f, 0f }, new[] { 0.5f, 0.866f }));

        var plan = await builder.ConstruirAsync(
            "dime cuales son los Planetas del Sistema Solar", 1, CancellationToken.None);

        Assert.DoesNotContain(plan.Pasos, p => p.CodigoHerramienta == "SqlQueryTool");
        Assert.Contains(plan.Pasos, p => p.Tipo == "Agent" && p.Nombre.Contains("Entregar"));
    }

    [Fact]
    public async Task ValorObservado_SalvaTablas_MantieneSql()
    {
        // Similitud baja (0.5) pero "mobiliario" es valor observado: se mantiene.
        var builder = CrearBuilder(new EmbFijo(new[] { 1f, 0f }, new[] { 0.5f, 0.866f }));

        var plan = await builder.ConstruirAsync(
            "cuantas son de categoria mobiliario?", 1, CancellationToken.None);

        Assert.Contains(plan.Pasos, p => p.CodigoHerramienta == "SqlQueryTool");
    }

    [Fact]
    public async Task CoincidenciaEsquema_SalvaTablas_MantieneSql()
    {
        // "Activos" en la pregunta (puntaje de esquema > 0): se mantiene aunque
        // la similitud sea baja.
        var builder = CrearBuilder(new EmbFijo(new[] { 1f, 0f }, new[] { 0.5f, 0.866f }));

        var plan = await builder.ConstruirAsync(
            "dame el total de registros de Activos?", 1, CancellationToken.None);

        Assert.Contains(plan.Pasos, p => p.CodigoHerramienta == "SqlQueryTool");
    }

    [Fact]
    public async Task DocumentoGanaAlEsquema_DescartaTablaAlucinada()
    {
        // Plan #9101: la pregunta puntúa 0.602 contra el catálogo (dentro de la
        // banda de las consultas SQL legítimas, así que un umbral absoluto NO la
        // descarta) pero 0.672 contra el documento que sí la responde. Gana el
        // documento → la tabla que inventó el LLM se descarta.
        var docs = RepoDocumentoAstronomia();
        docs.Setup(r => r.GetContenidosIndexadosAsync(It.IsAny<int>())).ReturnsAsync(new[]
        {
            new Asistente.Domain.Entities.DocumentoContenidoResumen
            {
                IdDocumento = 3,
                Nombre = "manual astronomico",
                Texto = "Distancia Tierra a Marte: 78 millones de km"
            }
        });

        var builder = CrearBuilder(new EmbeddingsPorFirma(0.602f, 0.672f), docs);
        var plan = await builder.ConstruirAsync(
            "segun astronomia , cual es la Distancia Tierra a Marte?", 1, CancellationToken.None);

        Assert.DoesNotContain(plan.Pasos, p => p.CodigoHerramienta == "SqlQueryTool");
    }

    [Fact]
    public async Task ValorObservado_RescataLaTablaQueLoContiene_Y_ConservaRag()
    {
        // Plan #9109 (pregunta mixta): "días de vacaciones por antigüedad (documento)
        // y productos de marca Dell (Activos.Marca='Dell')". El LLM solo.named
        // [Ventas] (sin columna Marca) y el documento ganaba el arbitraje, así que
        // la rama SQL desaparecía. Con el rescate por valor observado la tabla
        // correcta entra y, como hay valor, la puerta no descarta nada: SQL + RAG.
        var docs = RepoDocumentoVacaciones();
        docs.Setup(r => r.GetContenidosIndexadosAsync(It.IsAny<int>())).ReturnsAsync(new[]
        {
            new Asistente.Domain.Entities.DocumentoContenidoResumen
            {
                IdDocumento = 5,
                Nombre = "politica de vacaciones",
                Texto = "Dias de Vacaciones por Antiguedad: 1 a 3 anos 12 dias habiles"
            }
        });

        var executor = new Mock<ISqlQueryExecutor>();
        executor.Setup(e => e.ExecuteReadOnlyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<int>(), It.IsAny<CancellationToken>(), It.IsAny<int>()))
            .ReturnsAsync((string _c, string sql, object? _p, int _n, CancellationToken _t, int _x) =>
            {
                if (sql.Contains("SELECT DISTINCT"))
                {
                    if (sql.Contains("[Marca]"))
                        return new List<Dictionary<string, object?>> { Fila("V", "Dell"), Fila("V", "HP"), Fila("V", "Genérica") };
                    if (sql.Contains("[Categoria]"))
                        return new List<Dictionary<string, object?>> { Fila("V", "Mobiliario"), Fila("V", "Equipos de Cómputo") };
                    return new List<Dictionary<string, object?>>();
                }
                if (sql.Contains("DATA_TYPE IN"))
                    return new List<Dictionary<string, object?>>
                    {
                        Fila("COLUMN_NAME", "Nombre"), Fila("COLUMN_NAME", "Marca"),
                        Fila("COLUMN_NAME", "Categoria"), Fila("COLUMN_NAME", "Estado")
                    };
                if (sql.Contains("INFORMATION_SCHEMA.COLUMNS"))
                    return new List<Dictionary<string, object?>>
                    {
                        Fila("COLUMN_NAME", "IdActivo"), Fila("COLUMN_NAME", "Marca"), Fila("COLUMN_NAME", "Precio")
                    };
                return new List<Dictionary<string, object?>>();
            });

        var builder = new PlanBuilder(
            RepoAsistentes().Object,
            OllamaTablasActivos().Object,
            new Mock<ILogger<PlanBuilder>>().Object,
            RepoConexion().Object,
            RepoWorkflow().Object,
            new EmbeddingsPorFirma(0.581f, 0.704f),
            Cifrador().Object,
            executor.Object,
            docs.Object);

        var plan = await builder.ConstruirAsync(
            "dime los Dias de Vacaciones por Antiguedad, y los productos de marca Dell", 1, CancellationToken.None);

        Assert.Contains(plan.Pasos, p => p.CodigoHerramienta == "SqlQueryTool");
        Assert.Contains(plan.Pasos, p => p.CodigoHerramienta == "DocumentSearchTool");
        Assert.Contains(plan.Pasos, p => p.Nombre.Contains("Activos"));
    }

    private static Mock<IDocumentoRepository> RepoDocumentoVacaciones()
    {
        var repo = new Mock<IDocumentoRepository>();
        repo.Setup(r => r.GetAllAsync()).ReturnsAsync(Array.Empty<Documento>());
        return repo;
    }

    [Fact]
    public async Task EsquemaGanaAlDocumento_MantieneSql()
    {
        // Escenario inverso: 0.709 contra el catálogo y 0.512 contra el documento.
        // El documento se parece, pero la pregunta es de datos → SQL.
        var docs = RepoDocumentoAstronomia();
        docs.Setup(r => r.GetContenidosIndexadosAsync(It.IsAny<int>())).ReturnsAsync(new[]
        {
            new Asistente.Domain.Entities.DocumentoContenidoResumen
            {
                IdDocumento = 3,
                Nombre = "manual astronomico",
                Texto = "Distancia Tierra a Marte: 78 millones de km"
            }
        });

        var plan = await CrearBuilder(new EmbeddingsPorFirma(0.709f, 0.512f), docs).ConstruirAsync(
            "cuantos activos hay con precio mayor a 1000", 1, CancellationToken.None);

        Assert.Contains(plan.Pasos, p => p.CodigoHerramienta == "SqlQueryTool");
    }

    [Fact]
    public async Task SimilitudAlta_MantieneSql()
    {
        // Similitud 0.9 ≥ 0.55: dentro de dominio aunque no haya otras señales.
        var builder = CrearBuilder(new EmbFijo(new[] { 1f, 0f }, new[] { 0.9f, 0.436f }));

        var plan = await builder.ConstruirAsync(
            "dime cuales son los Planetas del Sistema Solar", 1, CancellationToken.None);

        Assert.Contains(plan.Pasos, p => p.CodigoHerramienta == "SqlQueryTool");
    }

    private static Mock<IOllamaService> OllamaTresTablas()
    {
        var mock = new Mock<IOllamaService>();
        mock.Setup(o => o.SendMessageAsync(It.IsAny<IEnumerable<Mensaje>>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<double?>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("{\"tablas\":[\"Activos\",\"Ventas\",\"Pedidos\"],\"rag\":true,\"reporte\":false,\"riesgo\":false,\"workflow\":false,\"agregacion\":false,\"aprobacion\":false,\"subconsultas\":[]}");
        mock.Setup(o => o.IsDisponibleAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        return mock;
    }

    private static Mock<IConexionBaseDatosRepository> RepoConexionTresTablas()
    {
        var repo = new Mock<IConexionBaseDatosRepository>();
        var con = new ConexionBaseDatos
        {
            IdConexion = 9,
            Nombre = "ControlActivosTest",
            BaseDatos = "ControlActivosTest",
            Activa = true,
            CadenaConexionCifrada = "x",
            TablasAutorizadas = new List<TablaAutorizada>
            {
                new() { NombreTabla = "Activos" }, new() { NombreTabla = "Ventas" },
                new() { NombreTabla = "Pedidos" }
            }
        };
        repo.Setup(r => r.GetActivasAsync()).ReturnsAsync(new[] { con });
        return repo;
    }

    /// <summary>Sin valores observados (DISTINCT vacío) y columnas idénticas para
    /// las tres tablas, de modo que el puntaje de esquema sea el mismo para todas.</summary>
    private static Mock<ISqlQueryExecutor> ExecutorSinValores()
    {
        var mock = new Mock<ISqlQueryExecutor>();
        mock.Setup(e => e.ExecuteReadOnlyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<int>(), It.IsAny<CancellationToken>(), It.IsAny<int>()))
            .ReturnsAsync((string _cs, string sql, object? _p, int _n, CancellationToken _ct, int _t) =>
            {
                if (sql.Contains("INFORMATION_SCHEMA.COLUMNS"))
                    return new List<Dictionary<string, object?>>
                    {
                        Fila("COLUMN_NAME", "IdActivo"), Fila("COLUMN_NAME", "Nombre"),
                        Fila("COLUMN_NAME", "Categoria"), Fila("COLUMN_NAME", "Precio")
                    };
                return new List<Dictionary<string, object?>>();
            });
        return mock;
    }

    [Fact]
    public async Task TresTablasSinAnclaje_SeDescartan_AunqueElDocumentoSeaRelevante()
    {
        // Plan #12197 (regresión): "hablame sobre el Cuerpo de Archivo segun ejemplo,
        // y sobre la Oferta Laboral". El LLM inventó [Insumos, Pedidos, Platos,
        // Activos, Ventas]; ninguna aparece en el texto ni es un valor observado.
        // El documento 'ejemplo' SÍ es relevante (0.630 ≥ 0.60) porque "Cuerpo de
        // Archivo" está en su texto, pero eso pertenece a la rama RAG: el margen
        // 0.630-0.575=0.055 queda apenas bajo el 0.06 y antes eso desactivaba la
        // regla de las 3+ tablas, dejando 5 consultas SQL fantasma.
        var docs = RepoDocumentoAstronomia();
        docs.Setup(r => r.GetContenidosIndexadosAsync(It.IsAny<int>())).ReturnsAsync(new[]
        {
            new Asistente.Domain.Entities.DocumentoContenidoResumen
            {
                IdDocumento = 3,
                Nombre = "manual astronomico",
                Texto = "Distancia Tierra a Marte: 78 millones de km"
            }
        });

        var builder = new PlanBuilder(
            RepoAsistentes().Object,
            OllamaTresTablas().Object,
            new Mock<ILogger<PlanBuilder>>().Object,
            RepoConexionTresTablas().Object,
            RepoWorkflow().Object,
            new EmbeddingsPorFirma(0.575f, 0.630f),
            Cifrador().Object,
            ExecutorSinValores().Object,
            docs.Object);

        var plan = await builder.ConstruirAsync(
            "hablame sobre el Cuerpo de Archivo segun ejemplo, y sobre la Oferta Laboral",
            1, CancellationToken.None);

        Assert.DoesNotContain(plan.Pasos, p => p.CodigoHerramienta == "SqlQueryTool");
        Assert.Contains(plan.Pasos, p => p.CodigoHerramienta == "DocumentSearchTool");
    }

    [Fact]
    public async Task TresTablasNombradasEnLaPregunta_MantienenSql()
    {
        // Contracara: si la pregunta NOMBRA las tablas hay anclaje nominal, así que
        // la puerta no se activa y la intención multi-tabla legítima sobrevive.
        var builder = new PlanBuilder(
            RepoAsistentes().Object,
            OllamaTresTablas().Object,
            new Mock<ILogger<PlanBuilder>>().Object,
            RepoConexionTresTablas().Object,
            RepoWorkflow().Object,
            new EmbeddingsPorFirma(0.575f, 0.630f),
            Cifrador().Object,
            ExecutorSinValores().Object,
            RepoDocumentoAstronomia().Object);

        var plan = await builder.ConstruirAsync(
            "compara activos, ventas y pedidos por precio", 1, CancellationToken.None);

        Assert.Contains(plan.Pasos, p => p.CodigoHerramienta == "SqlQueryTool");
    }
}

/// <summary>
/// Plan #13209: "dime los pedidos que repartio marco ruiz, y hablame sobre el
/// Trailer de Archivo". El SQL salia perfecto y el RAG desaparecia: la
/// similitud se media sobre el objetivo ENTERO, donde la mitad SQL domina el
/// vector y hunde la mitad documental (documento 0.631 contra esquema 0.649, sin
/// margen). Ahora se puntua la mejor clausula de ambos lados y la comparacion es
/// justa.
/// </summary>
public class PlanBuilderPreguntaMixtaSqlRagTests
{
    private static Asistente.Domain.Entities.Asistente Agente(int id) => new()
    { IdAsistente = id, Codigo = "AG", Nombre = "AG", Activo = true };

    private static Mock<IAsistenteRepository> RepoAsistentes()
    {
        var repo = new Mock<IAsistenteRepository>();
        var agentes = new List<Asistente.Domain.Entities.Asistente> { Agente(1008) };
        repo.Setup(r => r.GetAllAsync()).ReturnsAsync(agentes);
        repo.Setup(r => r.GetByIdAsync(It.IsAny<int>()))
            .ReturnsAsync((int id) => agentes.FirstOrDefault(a => a.IdAsistente == id));
        return repo;
    }

    private static Mock<IConexionBaseDatosRepository> RepoConexion()
    {
        var repo = new Mock<IConexionBaseDatosRepository>();
        var con = new ConexionBaseDatos
        {
            IdConexion = 9,
            Nombre = "ComidaTest",
            BaseDatos = "ComidaTest",
            Activa = true,
            CadenaConexionCifrada = "x",
            TablasAutorizadas = new List<TablaAutorizada> { new() { NombreTabla = "Pedidos" } }
        };
        repo.Setup(r => r.GetActivasAsync()).ReturnsAsync(new[] { con });
        return repo;
    }

    private static Mock<IWorkflowRepository> RepoWorkflow()
    {
        var repo = new Mock<IWorkflowRepository>();
        repo.Setup(r => r.GetActivosAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Workflow>());
        return repo;
    }

    /// <summary>El LLM clasifica como solo SQL: el rescate semántico es el que
    /// tiene que recuperar la rama documental.</summary>
    private static Mock<IOllamaService> OllamaSoloSql()
    {
        var mock = new Mock<IOllamaService>();
        mock.Setup(o => o.SendMessageAsync(It.IsAny<IEnumerable<Mensaje>>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<double?>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("{\"tablas\":[\"Pedidos\"],\"rag\":false,\"reporte\":false,\"riesgo\":false,\"workflow\":false,\"agregacion\":false,\"aprobacion\":false,\"subconsultas\":[]}");
        mock.Setup(o => o.IsDisponibleAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        return mock;
    }

    /// <summary>Coseno por cláusula: la parte de pedidos se parece al catálogo,
    /// la parte del Trailer se parece al documento.</summary>
    private sealed class EmbMixta : IEmbeddingProvider
    {
        public Task<float[]> GenerateEmbeddingAsync(string text)
        {
            if (text.Contains("IdPedido", StringComparison.OrdinalIgnoreCase))
                return Task.FromResult(Vector(0.66));      // firma de la tabla
            if (text.Contains("manual del Trailer", StringComparison.OrdinalIgnoreCase))
                return Task.FromResult(Vector(0.74));      // firma del documento
            if (text.Contains("repartio", StringComparison.OrdinalIgnoreCase))
                return Task.FromResult(Vector(0.65));
            if (text.Contains("Trailer", StringComparison.OrdinalIgnoreCase))
                return Task.FromResult(Vector(0.73));
            return Task.FromResult(Vector(0.60));
        }

        private static float[] Vector(double coseno)
            => new[] { (float)coseno, (float)Math.Sqrt(Math.Max(0, 1 - coseno * coseno)) };
    }

    private static Mock<IDocumentoRepository> RepoDocumento()
    {
        var repo = new Mock<IDocumentoRepository>();
        repo.Setup(r => r.GetAllAsync()).ReturnsAsync(Array.Empty<Documento>());
        repo.Setup(r => r.GetContenidosIndexadosAsync(It.IsAny<int>())).ReturnsAsync(new[]
        {
            new DocumentoContenidoResumen
            {
                IdDocumento = 2,
                Nombre = "manual del Trailer",
                Texto = "El trailer de un archivo PDF indica donde empieza la tabla de referencia cruzada."
            }
        });
        return repo;
    }

    private static Mock<ISqlQueryExecutor> ExecutorConValor()
    {
        var mock = new Mock<ISqlQueryExecutor>();
        mock.Setup(e => e.ExecuteReadOnlyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<int>(), It.IsAny<CancellationToken>(), It.IsAny<int>()))
            .ReturnsAsync((string _c, string sql, object? _p, int _n, CancellationToken _t, int _x) =>
            {
                if (sql.Contains("SELECT DISTINCT"))
                {
                    if (sql.Contains("[Repartidor]", StringComparison.OrdinalIgnoreCase))
                        return new List<Dictionary<string, object?>>
                        {
                            new() { ["V"] = "Marco Ruiz" }, new() { ["V"] = "Diego Torres" }
                        };
                    return new List<Dictionary<string, object?>>();
                }
                if (sql.Contains("INFORMATION_SCHEMA.COLUMNS"))
                    return new List<Dictionary<string, object?>>
                    {
                        new() { ["COLUMN_NAME"] = "IdPedido" }, new() { ["COLUMN_NAME"] = "Repartidor" },
                        new() { ["COLUMN_NAME"] = "Total" }
                    };
                return new List<Dictionary<string, object?>>();
            });
        return mock;
    }

    private static Mock<IConexionCifrador> Cifrador()
    {
        var mock = new Mock<IConexionCifrador>();
        mock.Setup(c => c.Descifrar(It.IsAny<string>())).Returns("Server=x");
        return mock;
    }

    [Fact]
    public void DividirEnClausulas_CortaPorPuntuacionYNuncaVacia()
    {
        var clausulas = PlanBuilder.DividirEnClausulas(
            "dime los pedidos que repartio marco ruiz, y hablame sobre el Trailer de Archivo").ToList();

        Assert.Equal(2, clausulas.Count);
        Assert.Contains("repartio", clausulas[0]);
        Assert.Contains("Trailer", clausulas[1]);
        Assert.Single(PlanBuilder.DividirEnClausulas("sin puntuacion"));
    }

    [Fact]
    public async Task MixtaSqlYDocumento_LlegaLaRamaDocumental()
    {
        var builder = new PlanBuilder(
            RepoAsistentes().Object,
            OllamaSoloSql().Object,
            new Mock<ILogger<PlanBuilder>>().Object,
            RepoConexion().Object,
            RepoWorkflow().Object,
            new EmbMixta(),
            Cifrador().Object,
            ExecutorConValor().Object,
            RepoDocumento().Object);

        var plan = await builder.ConstruirAsync(
            "dime los pedidos que repartio marco ruiz, y hablame sobre el Trailer de Archivo",
            1, CancellationToken.None);

        Assert.Contains(plan.Pasos, p => p.CodigoHerramienta == "SqlQueryTool");
        Assert.Contains(plan.Pasos, p => p.CodigoHerramienta == "DocumentSearchTool");
    }
}