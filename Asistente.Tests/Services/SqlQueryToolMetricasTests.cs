using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Asistente.Application.Services.Herramientas;
using Asistente.Domain.Entities;
using Asistente.Domain.Interfaces;
using Moq;
using Xunit;
using PerfilMetrica = Asistente.Application.Services.Herramientas.SqlQueryTool.PerfilMetrica;

namespace Asistente.Tests.Services;

/// <summary>
/// El importe de una venta es Σ(cantidad × precio), NO la Σ del precio unitario.
/// Antes SqlQueryTool sumaba la primera columna decimal de la tabla (PrecioUnitario
/// en `ventas`) y lo Presents como "Valor total": en los planes #6043/#6044 eso dio
/// 1320 / 2660 cuando el importe real era 4285 / 4445, y los agentes compararon
/// sobre cifras sin significado. Además no desglosaba por la dimensión comparada,
/// así que el LLM tenía que sumar a mano (y sumaba mal).
/// </summary>
public class SqlQueryToolMetricasTests
{
    /// <summary>Metadatos de `ventas`: IdVenta es PK, Cantidad entera no clave, PrecioUnitario decimal.</summary>
    private static List<Dictionary<string, object?>> ColumnasVentas() => new()
    {
        Fila("Col", "IdVenta", "Tipo", "int", "EsClave", "1"),
        Fila("Col", "Producto", "Tipo", "nvarchar", "EsClave", "0"),
        Fila("Col", "Categoria", "Tipo", "nvarchar", "EsClave", "0"),
        Fila("Col", "Cantidad", "Tipo", "int", "EsClave", "0"),
        Fila("Col", "PrecioUnitario", "Tipo", "decimal", "EsClave", "0"),
    };

    private static Dictionary<string, object?> Fila(params string[] pares)
    {
        var d = new Dictionary<string, object?>();
        for (int i = 0; i + 1 < pares.Length; i += 2) d[pares[i]] = pares[i + 1];
        return d;
    }

    /// <summary>
    /// Herramienta con un esquema `ventas` y valores observados, ejecutando el SQL real
    /// contra un verificador en memoria. No toca base de datos.
    /// </summary>
    private static SqlQueryTool CrearTool(
        Action<string>? alEjecutar = null,
        List<Dictionary<string, object?>>? resultadoAgregado = null,
        IOllamaService? ollama = null)
    {
        var conexion = new ConexionBaseDatos
        {
            IdConexion = 1,
            Nombre = "VentasTest",
            BaseDatos = "VentasTest",
            CadenaConexionCifrada = "x",
            Activa = true,
            TablasAutorizadas = new List<TablaAutorizada> { new() { NombreTabla = "ventas" } }
        };

        var conexionRepo = new Mock<IConexionBaseDatosRepository>();
        conexionRepo.Setup(r => r.GetActivasAsync())
            .ReturnsAsync(new List<ConexionBaseDatos> { conexion });

        var cifrador = new Mock<IConexionCifrador>();
        cifrador.Setup(c => c.Descifrar(It.IsAny<string>())).Returns("Server=mock;");

        var executor = new Mock<ISqlQueryExecutor>();
        executor.Setup(e => e.ExecuteReadOnlyAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object?>(),
                It.IsAny<int>(), It.IsAny<CancellationToken>(), It.IsAny<int>()))
            .ReturnsAsync((string _c, string sql, object? _p, int _n, CancellationToken _ct, int _t) =>
            {
                alEjecutar?.Invoke(sql);

                // 1) Perfil métrico: pide la marca EsClave.
                if (sql.Contains("EsClave", StringComparison.Ordinal))
                    return ColumnasVentas();

                // 2) Columnas de texto para el mapa de valores observados.
                if (sql.Contains("DATA_TYPE IN ('char'", StringComparison.Ordinal))
                    return ColumnasVentas()
                        .Where(c => (string?)c["Tipo"] == "nvarchar")
                        .Select(c => Fila("COLUMN_NAME", (string)c["Col"]!))
                        .ToList();

                // 3) DISTINCT de valores por columna: aquí aparecen las categorías reales.
                if (sql.Contains("SELECT DISTINCT", StringComparison.OrdinalIgnoreCase))
                {
                    if (sql.Contains("[Categoria]", StringComparison.Ordinal))
                        return new List<Dictionary<string, object?>>
                        {
                            Fila("V", "Ropa"), Fila("V", "Hogar"), Fila("V", "Electrónica")
                        };
                    return new List<Dictionary<string, object?>>();
                }

                // 4) Agregación agrupada.
                if (sql.Contains(" AS Grupo", StringComparison.Ordinal))
                    return resultadoAgregado ?? new List<Dictionary<string, object?>>();

                // 5) Filas de la consulta principal.
                return new List<Dictionary<string, object?>>
                {
                    Fila("IdVenta", "8", "Producto", "Camisa Algodón Slim", "Categoria", "Ropa", "Cantidad", "8", "PrecioUnitario", "75.00")
                };
            });

        return new SqlQueryTool(conexionRepo.Object,
            new Mock<ITablaAutorizadaRepository>().Object,
            new Mock<IVistaAutorizadaRepository>().Object,
            cifrador.Object, executor.Object,
            new Mock<Microsoft.Extensions.Logging.ILogger<SqlQueryTool>>().Object,
            ollama: ollama);
    }

    private static ToolExecutionRequest Request(string pregunta, string modo) => new()
    {
        HerramientaCodigo = "SqlQueryTool",
        Parametros = new Dictionary<string, object?> { ["pregunta"] = pregunta, ["modo"] = modo }
    };

    [Fact]
    public async Task Importe_UsaCantidadTimesPrecio_NoLaSumaDePrecioUnitario()
    {
        var sqls = new List<string>();
        var tool = CrearTool(sqls.Add, new List<Dictionary<string, object?>>
        {
            Fila("Grupo", "Ropa", "Total", "7", "Unidades", "25", "ValorTotal", "4285.00"),
            Fila("Grupo", "Hogar", "Total", "7", "Unidades", "22", "ValorTotal", "4445.00"),
        });

        var res = await tool.ExecuteAsync(
            Request("Consulta las ventas de Ropa y las ventas de Hogar en paralelo, luego compara ambas categorias", "CONSULTA"),
            CancellationToken.None);

        Assert.True(res.Exitoso, res.Error);

        // La agregación multiplica cantidad por precio: ese es el importe.
        var agregacion = sqls.First(s => s.Contains(" AS Grupo"));
        Assert.Contains("SUM([Cantidad] * [PrecioUnitario]) AS ValorTotal", agregacion);
        Assert.Contains("SUM([Cantidad]) AS Unidades", agregacion);

        // Y NO puede ser la suma suelta de la columna decimal.
        Assert.DoesNotContain("SUM([PrecioUnitario])", agregacion);
    }

    [Fact]
    public async Task Desglosa_PorLaMismaColumnaQueSeFiltro()
    {
        var sqls = new List<string>();
        var tool = CrearTool(sqls.Add, new List<Dictionary<string, object?>>
        {
            Fila("Grupo", "Ropa", "Total", "7", "Unidades", "25", "ValorTotal", "4285.00"),
        });

        await tool.ExecuteAsync(
            Request("Consulta las ventas de Ropa y las ventas de Hogar en paralelo, luego compara ambas categorias", "CONSULTA"),
            CancellationToken.None);

        var agregacion = sqls.First(s => s.Contains(" AS Grupo"));
        Assert.Contains("GROUP BY [Categoria]", agregacion);
        Assert.Contains("[Categoria] AS Grupo", agregacion);
    }

    [Fact]
    public async Task Contenido_PresentaElDesgloseConCifrasDeSql()
    {
        var tool = CrearTool(alEjecutar: null, resultadoAgregado: new List<Dictionary<string, object?>>
        {
            Fila("Grupo", "Ropa", "Total", "7", "Unidades", "25", "ValorTotal", "4285.00"),
            Fila("Grupo", "Hogar", "Total", "7", "Unidades", "22", "ValorTotal", "4445.00"),
        });

        var res = await tool.ExecuteAsync(
            Request("Consulta las ventas de Ropa y las ventas de Hogar en paralelo, luego compara ambas categorias", "CONSULTA"),
            CancellationToken.None);

        Assert.Contains("Desglose por Categoria", res.Contenido);
        Assert.Contains("Categoria = Ropa", res.Contenido);
        Assert.Contains("25 unidades", res.Contenido);
        // Etiqueta honesta: con cantidad y precio la métrica SÍ es un importe.
        Assert.Contains("importe total 4285.00", res.Contenido);
        Assert.Contains("Categoria = Hogar", res.Contenido);
        Assert.Contains("importe total 4445.00", res.Contenido);
    }

    [Fact]
    public async Task SinColumnaCantidad_NoFabricaImporte_SoloSumaDecimal()
    {
        // Tabla sin columna entera no clave: no se puede multiplicar, así que la
        // métrica es la suma de la decimal y NO se etiqueta como importe.
        var tool = new SqlQueryTool(
            new Mock<IConexionBaseDatosRepository>().Object,
            new Mock<ITablaAutorizadaRepository>().Object,
            new Mock<IVistaAutorizadaRepository>().Object,
            new Mock<IConexionCifrador>().Object,
            new Mock<ISqlQueryExecutor>().Object,
            new Mock<Microsoft.Extensions.Logging.ILogger<SqlQueryTool>>().Object);

        // Sin conexiones activas: debe fallar limpio, no inventar una métrica.
        var res = await tool.ExecuteAsync(Request("dame el total de ventas", "ANALISIS"), CancellationToken.None);
        Assert.False(res.Exitoso);
    }

    /// <summary>
    /// Una tabla con decimal pero sin cantidad NO puede presentar su suma como dinero:
    /// la etiqueta debe decirlo, no llamarlo "importe total".
    /// </summary>
    [Fact]
    public void EtiquetaMetrica_NoLlamaImporteSiNoHayCantidad()
    {
        var conCantidad = new PerfilMetrica("Cantidad", "PrecioUnitario");
        var soloDecimal = new PerfilMetrica(null, "PrecioUnitario");

        Assert.True(conCantidad.HayImporte);
        Assert.Equal("importe total", conCantidad.Etiqueta);
        Assert.Equal("SUM([Cantidad] * [PrecioUnitario])", conCantidad.ExpresionImporte);

        Assert.False(soloDecimal.HayImporte);
        Assert.Equal("suma de la columna decimal", soloDecimal.Etiqueta);
        Assert.Equal("SUM([PrecioUnitario])", soloDecimal.ExpresionImporte);
    }

    /// <summary>Sin columna de precio solo hay conteo: no se fabrica expresión de importe.</summary>
    [Fact]
    public void PerfilSinColumnasNumericas_NoTieneExpresionDeImporte()
    {
        var perfil = new PerfilMetrica(null, null);
        Assert.False(perfil.HayImporte);
        Assert.Null(perfil.ExpresionImporte);
        Assert.Null(perfil.ExpresionCantidad);
    }

    private static IOllamaService OllamaDevuelve(string texto)
    {
        var mock = new Mock<IOllamaService>();
        mock.Setup(o => o.SendMessageAsync(
                It.IsAny<IEnumerable<Mensaje>>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<double?>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(texto);
        return mock.Object;
    }

    /// <summary>
    /// Plan #7058: la pregunta trae dígitos y la tabla columnas numéricas. Aunque la
    /// vía rápida encuentre un filtro de texto, el SQL semántico del LLM (condición
    /// numérica) prevalece. Cero vocabulario: solo dígitos + tipos del esquema.
    /// </summary>
    [Fact]
    public async Task ConDigitosYColumnasNumericas_PriorizaSqlSemanticoDelLLM()
    {
        var sqls = new List<string>();
        var tool = CrearTool(sqls.Add, null,
            OllamaDevuelve("SELECT * FROM [ventas] WHERE [PrecioUnitario] > 100;"));

        var res = await tool.ExecuteAsync(
            Request("Consulta las ventas de Ropa con precio superior a 100", "CONSULTA"),
            CancellationToken.None);

        Assert.True(res.Exitoso, res.Error);
        Assert.Contains(sqls, s => s.Contains("[PrecioUnitario] > 100"));
    }

    /// <summary>
    /// Respaldo intacto: si el LLM no devuelve SQL válido, la vía rápida de texto
    /// funciona exactamente igual que antes.
    /// </summary>
    [Fact]
    public async Task SiLlmFalla_ConDigitos_UsaViaRapidaComoAntes()
    {
        var sqls = new List<string>();
        var tool = CrearTool(sqls.Add, null, OllamaDevuelve("no hay sql aquí"));

        var res = await tool.ExecuteAsync(
            Request("Consulta las ventas de Ropa con precio superior a 100", "CONSULTA"),
            CancellationToken.None);

        Assert.True(res.Exitoso, res.Error);
        Assert.Contains(sqls, s => s.Contains("TOP 10") && s.Contains("[Categoria] = 'Ropa'"));
    }
}
