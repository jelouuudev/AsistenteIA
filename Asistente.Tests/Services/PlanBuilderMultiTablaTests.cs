using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
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
/// Plan #9130: "activos de categoria vehiculos, bono vacacional, productos
/// vendidos en tienda fisica, todo en pdf". Tres intenciones (SQL Activos + RAG +
/// SQL Ventas) + informe. El Planner solo generaba el primer SQL: creaba un único
/// paso para la primera tabla y el RAG pedía ganarle al esquema por margen aunque
/// la pregunta ya demostraba varias intenciones. Ahora hay un paso SQL por tabla
/// (cada uno con su tabla en la Entrada) y RAG con documento relevante.
/// </summary>
public class PlanBuilderMultiTablaTests
{
    private sealed class EmbTresVias : IEmbeddingProvider
    {
        private readonly float[] _pregunta = { 1f, 0f };
        private readonly float[] _tabla;
        private readonly float[] _documento;
        public EmbTresVias(float cosTabla, float cosDocumento)
        {
            _tabla = new[] { cosTabla, (float)Math.Sqrt(1 - cosTabla * cosTabla) };
            _documento = new[] { cosDocumento, (float)Math.Sqrt(1 - cosDocumento * cosDocumento) };
        }
        public Task<float[]> GenerateEmbeddingAsync(string text)
        {
            if (text.Contains("IdActivo") || text.Contains("IdVenta") || text.Contains("IdInsumo")) return Task.FromResult(_tabla);
            if (text.Contains("politica de vacaciones")) return Task.FromResult(_documento);
            return Task.FromResult(_pregunta);
        }
    }

    private static Asistente.Domain.Entities.Asistente Agente() => new()
    {
        IdAsistente = 1, Codigo = "PRINCIPAL", Nombre = "Principal", Activo = true
    };

    private static Mock<IAsistenteRepository> RepoAsistentes()
    {
        var repo = new Mock<IAsistenteRepository>();
        var agente = Agente();
        repo.Setup(r => r.GetAllAsync()).ReturnsAsync(new[] { agente });
        repo.Setup(r => r.GetByIdAsync(It.IsAny<int>())).ReturnsAsync(agente);
        return repo;
    }

    private static ConexionBaseDatos ConexionActivos() => new()
    {
        IdConexion = 1, Nombre = "ControlActivosTest", BaseDatos = "ControlActivosTest",
        Activa = true, CadenaConexionCifrada = "x",
        TablasAutorizadas = new List<TablaAutorizada> { new() { NombreTabla = "Activos" } }
    };

    private static ConexionBaseDatos ConexionVentas() => new()
    {
        IdConexion = 2, Nombre = "VentasTest", BaseDatos = "VentasTest",
        Activa = true, CadenaConexionCifrada = "x",
        TablasAutorizadas = new List<TablaAutorizada> { new() { NombreTabla = "Ventas" } }
    };

    private static ConexionBaseDatos ConexionComida() => new()
    {
        IdConexion = 3, Nombre = "ComidaTest", BaseDatos = "ComidaTest",
        Activa = true, CadenaConexionCifrada = "x",
        TablasAutorizadas = new List<TablaAutorizada> { new() { NombreTabla = "Insumos" } }
    };

    private static Mock<IConexionCifrador> Cifrador()
    {
        var mock = new Mock<IConexionCifrador>();
        mock.Setup(c => c.Descifrar(It.IsAny<string>())).Returns("Server=x");
        return mock;
    }

    private static Dictionary<string, object?> Fila(string col, string val)
        => new() { [col] = val };

    private static Mock<ISqlQueryExecutor> Executor()
    {
        var mock = new Mock<ISqlQueryExecutor>();
        mock.Setup(e => e.ExecuteReadOnlyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<int>(), It.IsAny<CancellationToken>(), It.IsAny<int>()))
            .ReturnsAsync((string _c, string sql, object? _p, int _n, CancellationToken _t, int _x) =>
            {
                // La tabla de las consultas de catálogo viaja como parámetro @t,
                // no en el texto SQL.
                var tablaParam = (_p as Dictionary<string, object?>)?.TryGetValue("t", out var tv) == true
                    ? tv?.ToString() ?? string.Empty
                    : string.Empty;
                if (sql.Contains("SELECT DISTINCT"))
                {
                    if (sql.Contains("[Activos]") && sql.Contains("[Categoria]"))
                        return new List<Dictionary<string, object?>> { Fila("V", "Vehículos"), Fila("V", "Mobiliario") };
                    if (sql.Contains("[Ventas]") && sql.Contains("[CanalVenta]"))
                        return new List<Dictionary<string, object?>> { Fila("V", "Tienda Física"), Fila("V", "En Línea") };
                    if (sql.Contains("[Insumos]") && sql.Contains("[Unidad]"))
                        return new List<Dictionary<string, object?>> { Fila("V", "Litros"), Fila("V", "Kilos") };
                    return new List<Dictionary<string, object?>>();
                }
                if (sql.Contains("DATA_TYPE IN"))
                    return tablaParam.Equals("Activos", StringComparison.OrdinalIgnoreCase)
                        ? new List<Dictionary<string, object?>> { Fila("COLUMN_NAME", "Categoria") }
                        : tablaParam.Equals("Ventas", StringComparison.OrdinalIgnoreCase)
                            ? new List<Dictionary<string, object?>> { Fila("COLUMN_NAME", "CanalVenta") }
                            : new List<Dictionary<string, object?>> { Fila("COLUMN_NAME", "Unidad") };
                if (sql.Contains("INFORMATION_SCHEMA.COLUMNS"))
                    return tablaParam.Equals("Activos", StringComparison.OrdinalIgnoreCase)
                        ? new List<Dictionary<string, object?>>
                        {
                            Fila("COLUMN_NAME", "IdActivo"), Fila("COLUMN_NAME", "Categoria"), Fila("COLUMN_NAME", "Precio")
                        }
                        : tablaParam.Equals("Ventas", StringComparison.OrdinalIgnoreCase)
                            ? new List<Dictionary<string, object?>>
                            {
                                Fila("COLUMN_NAME", "IdVenta"), Fila("COLUMN_NAME", "CanalVenta"), Fila("COLUMN_NAME", "Cantidad")
                            }
                            : new List<Dictionary<string, object?>>
                            {
                                Fila("COLUMN_NAME", "IdInsumo"), Fila("COLUMN_NAME", "Unidad"), Fila("COLUMN_NAME", "CostoUnitario")
                            };
                return new List<Dictionary<string, object?>>();
            });
        return mock;
    }

    private static Mock<IOllamaService> OllamaMixta()
    {
        var mock = new Mock<IOllamaService>();
        mock.Setup(o => o.SendMessageAsync(It.IsAny<IEnumerable<Mensaje>>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<double?>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("{\"tablas\":[\"Activos\",\"Ventas\"],\"rag\":false,\"reporte\":true,\"riesgo\":false,\"workflow\":false,\"agregacion\":true,\"aprobacion\":false,\"subconsultas\":[]}");
        mock.Setup(o => o.IsDisponibleAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        return mock;
    }

    private static Mock<IOllamaService> OllamaTresTablas()
    {
        var mock = new Mock<IOllamaService>();
        mock.Setup(o => o.SendMessageAsync(It.IsAny<IEnumerable<Mensaje>>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<double?>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("{\"tablas\":[\"Activos\",\"Ventas\",\"Insumos\"],\"rag\":false,\"reporte\":false,\"riesgo\":false,\"workflow\":false,\"agregacion\":false,\"aprobacion\":false,\"subconsultas\":[]}");
        mock.Setup(o => o.IsDisponibleAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        return mock;
    }

    private static Mock<IDocumentoRepository> RepoDocumentos()
    {
        var repo = new Mock<IDocumentoRepository>();
        repo.Setup(r => r.GetAllAsync()).ReturnsAsync(Array.Empty<Documento>());
        repo.Setup(r => r.GetContenidosIndexadosAsync(It.IsAny<int>())).ReturnsAsync(new[]
        {
            new DocumentoContenidoResumen
            {
                IdDocumento = 5, Nombre = "politica de vacaciones",
                Texto = "Bono Vacacional: monto 300 USD por periodo vacacional"
            }
        });
        return repo;
    }

    private static Mock<IConexionBaseDatosRepository> RepoConexiones()
    {
        var repo = new Mock<IConexionBaseDatosRepository>();
        repo.Setup(r => r.GetActivasAsync()).ReturnsAsync(new[] { ConexionActivos(), ConexionVentas() });
        return repo;
    }

    private static Mock<IConexionBaseDatosRepository> RepoTresConexiones()
    {
        var repo = new Mock<IConexionBaseDatosRepository>();
        repo.Setup(r => r.GetActivasAsync()).ReturnsAsync(new[] { ConexionActivos(), ConexionVentas(), ConexionComida() });
        return repo;
    }

    private static PlanBuilder Crear(float cosTabla, float cosDocumento) => new(
        RepoAsistentes().Object,
        OllamaMixta().Object,
        new Mock<ILogger<PlanBuilder>>().Object,
        RepoConexiones().Object,
        new Mock<IWorkflowRepository>().Object,
        new EmbTresVias(cosTabla, cosDocumento),
        Cifrador().Object,
        Executor().Object,
        RepoDocumentos().Object);

    private static PlanBuilder CrearTresTablas(float cosTabla, float cosDocumento) => new(
        RepoAsistentes().Object,
        OllamaTresTablas().Object,
        new Mock<ILogger<PlanBuilder>>().Object,
        RepoTresConexiones().Object,
        new Mock<IWorkflowRepository>().Object,
        new EmbTresVias(cosTabla, cosDocumento),
        Cifrador().Object,
        Executor().Object,
        RepoDocumentos().Object);

    private const string Objetivo9130 =
        "dime cuantos activos son de categoria vehiculos, y hablame sobre el bono vacacional, y todos los productos vendidos a traves de tienda fisica, y todo eso en un pdf";

    [Fact]
    public async Task TresIntenciones_UnPasoSqlPorTabla_MasRagMasInforme()
    {
        // Documento 0.706 vs esquema 0.681: NO gana por margen, pero con dos tablas
        // la pregunta ya es multi-intención y el documento es relevante.
        var plan = await Crear(0.681f, 0.706f).ConstruirAsync(Objetivo9130, 1, CancellationToken.None);

        var sql = plan.Pasos.Where(p => p.CodigoHerramienta == "SqlQueryTool" && p.Nombre.StartsWith("Consultar datos")).ToList();
        Assert.Equal(2, sql.Count);
        Assert.Contains(sql, p => p.Nombre.Contains("Activos"));
        Assert.Contains(sql, p => p.Nombre.Contains("Ventas"));
        // Cada paso SQL lleva su tabla en la Entrada (contrato máquina-máquina).
        foreach (var paso in sql)
        {
            using var doc = JsonDocument.Parse(paso.Entrada!);
            var tabla = doc.RootElement.GetProperty("tabla").GetString();
            Assert.True(paso.Nombre.Contains(tabla), $"El paso '{paso.Nombre}' no coincide con su tabla '{tabla}'");
        }
        Assert.Contains(plan.Pasos, p => p.CodigoHerramienta == "DocumentSearchTool");
        Assert.Contains(plan.Pasos, p => p.CodigoHerramienta == "ReportTool");
    }

    [Fact]
    public async Task DocumentoIrrelevante_NoActivaRag_AunqueHayaDosTablas()
    {
        // Documento 0.51: por debajo del umbral aunque haya dos intenciones SQL.
        var plan = await Crear(0.681f, 0.51f).ConstruirAsync(Objetivo9130, 1, CancellationToken.None);

        Assert.Equal(2, plan.Pasos.Count(p => p.CodigoHerramienta == "SqlQueryTool" && p.Nombre.StartsWith("Consultar datos")));
        Assert.DoesNotContain(plan.Pasos, p => p.CodigoHerramienta == "DocumentSearchTool");
    }

    [Fact]
    public async Task TresTablasSinAnclaje_SeDescartanPorAlucinacion()
    {
        // Plan #9157 ("quien escribio don quijote de la mancha?"): el LLM nombra
        // las 3 tablas sin anclaje nominal, de valor ni documental. La similitud
        // con el esquema (0.60) está DENTRO de la banda legítima, así que el
        // umbral absoluto no la tumba: la descarta el patrón de alucinación
        // (3+ tablas sin anclaje).
        var plan = await CrearTresTablas(0.60f, 0.55f).ConstruirAsync(
            "quien escribio don quijote de la mancha?", 1, CancellationToken.None);

        Assert.DoesNotContain(plan.Pasos, p => p.CodigoHerramienta == "SqlQueryTool");
        Assert.DoesNotContain(plan.Pasos, p => p.CodigoHerramienta == "DocumentSearchTool");
    }

    [Fact]
    public async Task TresTablasConValor_SeConservan_LasTres()
    {
        // Tres intenciones genuinas con valores en las 3 tablas: el rescate no
        // recorta a 2 y la puerta no las toca (hay anclaje de valor).
        var plan = await CrearTresTablas(0.65f, 0.51f).ConstruirAsync(
            "dame vehiculos y ventas de tienda fisica e insumos en litros", 1, CancellationToken.None);

        var sql = plan.Pasos.Where(p => p.CodigoHerramienta == "SqlQueryTool" && p.Nombre.StartsWith("Consultar datos")).ToList();
        Assert.Equal(3, sql.Count);
        Assert.Contains(sql, p => p.Nombre.Contains("Activos"));
        Assert.Contains(sql, p => p.Nombre.Contains("Ventas"));
        Assert.Contains(sql, p => p.Nombre.Contains("Insumos"));
        Assert.DoesNotContain(plan.Pasos, p => p.CodigoHerramienta == "DocumentSearchTool");
    }
}