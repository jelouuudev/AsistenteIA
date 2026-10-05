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

namespace Asistente.Tests.Services;

/// <summary>
/// Regresión del plan #9062: "de cuántos activos el precio supera los 1000" generó
/// WHERE [Estado]='ACTIVO' AND [Precio]=1000. Dos fallos: (1) "activos" nombraba la
/// TABLA, no un filtro de Estado; (2) el operador era "=" en vez de "&gt;".
/// Tras el fix el SQL es WHERE [Precio] &gt; 1000 (16 filas) y el contenido muestra
/// el filtro aplicado. Cero vocabulario de dominio: la tabla sale del esquema y el
/// operador del micro-LLM/estructura.
/// </summary>
public class FiltroMencionTablaTests
{
    [Theory]
    [InlineData("ACTIVO", "Activos", true)]
    [InlineData("activo", "Activos", true)]
    [InlineData("Ropa", "Ventas", false)]
    [InlineData("Juan Perez", "Activos", false)]
    [InlineData("Lima", "Ventas", false)]
    [InlineData("ventas", "Ventas", true)]
    public void EsMencionDeTabla_DetectaNombreDeTabla_NoValores(string literal, string tabla, bool esperado)
        => Assert.Equal(esperado, SqlQueryTool.EsMencionDeTabla(literal, tabla));

    private static Dictionary<string, object?> Fila(params string[] pares)
    {
        var d = new Dictionary<string, object?>();
        for (int i = 0; i + 1 < pares.Length; i += 2) d[pares[i]] = pares[i + 1];
        return d;
    }

    /// <summary>Esquema simplificado de ControlActivosTest: una sola decimal (Precio).</summary>
    private static List<Dictionary<string, object?>> ColumnasActivos() => new()
    {
        Fila("Col", "IdActivo", "Tipo", "int", "EsClave", "1"),
        Fila("Col", "Nombre", "Tipo", "nvarchar", "EsClave", "0"),
        Fila("Col", "Estado", "Tipo", "nvarchar", "EsClave", "0"),
        Fila("Col", "Precio", "Tipo", "decimal", "EsClave", "0"),
    };

    private static SqlQueryTool CrearToolActivos(List<string> sqls)
    {
        var conexion = new ConexionBaseDatos
        {
            IdConexion = 2,
            Nombre = "ControlActivosTest",
            BaseDatos = "ControlActivosTest",
            CadenaConexionCifrada = "x",
            Activa = true,
            TablasAutorizadas = new List<TablaAutorizada> { new() { NombreTabla = "Activos" } }
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
                sqls.Add(sql);

                if (sql.Contains("EsClave", StringComparison.Ordinal))
                    return ColumnasActivos();

                if (sql.Contains("DATA_TYPE IN ('char'", StringComparison.Ordinal))
                    return ColumnasActivos()
                        .Where(c => (string?)c["Tipo"] == "nvarchar")
                        .Select(c => Fila("COLUMN_NAME", (string)c["Col"]!))
                        .ToList();

                if (sql.Contains("SELECT DISTINCT", StringComparison.OrdinalIgnoreCase))
                {
                    if (sql.Contains("[Estado]", StringComparison.Ordinal))
                        return new List<Dictionary<string, object?>>
                        {
                            Fila("V", "ACTIVO"), Fila("V", "INACTIVO")
                        };
                    return new List<Dictionary<string, object?>>();
                }

                if (sql.Contains("COUNT(*)", StringComparison.Ordinal))
                    return new List<Dictionary<string, object?>>
                    {
                        Fila("Total", "16", "ValorTotal", "654700.00")
                    };

                return new List<Dictionary<string, object?>>
                {
                    Fila("IdActivo", "1", "Nombre", "Laptop Dell", "Estado", "ACTIVO", "Precio", "4500.00")
                };
            });

        // Sin Ollama ni embeddings: columna por metadatos (única decimal) y operador
        // por defecto ">" (sin símbolos en la pregunta). Nada de vocabulario.
        return new SqlQueryTool(conexionRepo.Object,
            new Mock<ITablaAutorizadaRepository>().Object,
            new Mock<IVistaAutorizadaRepository>().Object,
            cifrador.Object, executor.Object,
            new Mock<Microsoft.Extensions.Logging.ILogger<SqlQueryTool>>().Object);
    }

    [Fact]
    public void PluralEn_otra_Columna_NoSe_Pierde_Ante_una_Exacta()
    {
        // Plan #9121: "productos entregados en Lima" → Ciudad='Lima' (exacta) y
        // Estado='Entregado' (plural de "entregados"). Antes la coincidencia exacta
        // de Ciudad apagaba todas las plurales y se consultaba solo por Ciudad.
        var mapa = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Ventas.Ciudad"] = new() { "Lima", "Arequipa" },
            ["Ventas.Estado"] = new() { "Entregado", "Pendiente" }
        };
        var exactos = new List<ValorDetectado>();
        var plurales = new List<ValorDetectado>();

        FiltroSemantico.Detectar("cuantos productos fueron entregados en lima", mapa, exactos, plurales);
        var porColumna = FiltroSemantico.ElegirPorColumna(exactos, plurales);

        Assert.Equal(2, porColumna.Count);
        Assert.Contains(porColumna["Estado"], v => v.Literal == "Entregado");
        Assert.Contains(porColumna["Ciudad"], v => v.Literal == "Lima");
        // La clave completa dice de qué tabla es cada valor.
        Assert.Equal("Ventas.Estado", porColumna["Estado"].Single().Clave);
        Assert.Equal("Ventas.Ciudad", porColumna["Ciudad"].Single().Clave);
    }

    [Fact]
    public void Exacto_Tiene_Prioridad_Que_Plural_en_la_Misma_Columna()
    {
        var mapa = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Activos.Estado"] = new() { "ACTIVO", "INACTIVO" }
        };
        var exactos = new List<ValorDetectado>();
        var plurales = new List<ValorDetectado>();

        FiltroSemantico.Detectar("cuantos activos", mapa, exactos, plurales);
        var porColumna = FiltroSemantico.ElegirPorColumna(exactos, plurales);

        // Un solo valor por columna: el exacto descarta al plural equivalente
        // para no duplicar la condición.
        Assert.Single(porColumna);
        Assert.Equal("ACTIVO", porColumna.Values.First().Single().Literal);
    }

    [Fact]
    public async Task TablaSugerida_Valida_GanaSobreLaSimilitud()
    {
        // Plan #9130: cada paso SQL trae la tabla que el plan resolvió. Aunque la
        // pregunta mencione valores de otra tabla ("Dell" → Activos), la sugerencia
        // validada manda y no se re-adivina.
        var sqls = new List<string>();
        var tool = CrearToolDosConexiones(sqls);

        var res = await tool.ExecuteAsync(new ToolExecutionRequest
        {
            HerramientaCodigo = "SqlQueryTool",
            Parametros = new Dictionary<string, object?>
            {
                ["pregunta"] = "dime los productos de marca Dell",
                ["tabla"] = "Ventas",
                ["modo"] = "CONSULTA"
            }
        }, CancellationToken.None);

        Assert.True(res.Exitoso, res.Error);
        Assert.Contains(sqls, s => s.Contains("FROM [Ventas]", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task TablaSugerida_NoAutorizada_SeIgnora_YDecideLaHerramienta()
    {
        // Una sugerencia que no está autorizada no puede forzar nada: se ignora y
        // la herramienta elige por valor observado (Activos, por "Dell").
        var sqls = new List<string>();
        var tool = CrearToolDosConexiones(sqls);

        var res = await tool.ExecuteAsync(new ToolExecutionRequest
        {
            HerramientaCodigo = "SqlQueryTool",
            Parametros = new Dictionary<string, object?>
            {
                ["pregunta"] = "dime los productos de marca Dell",
                ["tabla"] = "Inventario",
                ["modo"] = "CONSULTA"
            }
        }, CancellationToken.None);

        Assert.True(res.Exitoso, res.Error);
        Assert.Contains(sqls, s => s.Contains("FROM [Activos]", StringComparison.OrdinalIgnoreCase));
    }

    private static SqlQueryTool CrearToolDosConexiones(List<string> sqls)
    {
        var conActivos = new ConexionBaseDatos
        {
            IdConexion = 1, Nombre = "ControlActivosTest", BaseDatos = "ControlActivosTest",
            Activa = true, CadenaConexionCifrada = "x",
            TablasAutorizadas = new List<TablaAutorizada> { new() { NombreTabla = "Activos" } }
        };
        var conVentas = new ConexionBaseDatos
        {
            IdConexion = 2, Nombre = "VentasTest", BaseDatos = "VentasTest",
            Activa = true, CadenaConexionCifrada = "x",
            TablasAutorizadas = new List<TablaAutorizada> { new() { NombreTabla = "Ventas" } }
        };
        var conexionRepo = new Mock<IConexionBaseDatosRepository>();
        conexionRepo.Setup(r => r.GetActivasAsync())
            .ReturnsAsync(new List<ConexionBaseDatos> { conActivos, conVentas });

        var cifrador = new Mock<IConexionCifrador>();
        cifrador.Setup(c => c.Descifrar(It.IsAny<string>())).Returns("Server=mock;");

        var executor = new Mock<ISqlQueryExecutor>();
        executor.Setup(e => e.ExecuteReadOnlyAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object?>(),
                It.IsAny<int>(), It.IsAny<CancellationToken>(), It.IsAny<int>()))
            .ReturnsAsync((string _c, string sql, object? _p, int _n, CancellationToken _ct, int _t) =>
            {
                sqls.Add(sql);
                var tablaParam = (_p as Dictionary<string, object?>)?.TryGetValue("t", out var tv) == true
                    ? tv?.ToString() ?? string.Empty : string.Empty;

                if (sql.Contains("EsClave", StringComparison.Ordinal))
                    return new List<Dictionary<string, object?>>
                    {
                        Fila("Col", "IdActivo", "Tipo", "int"), Fila("Col", "Marca", "Tipo", "nvarchar")
                    };
                if (sql.Contains("DATA_TYPE IN ('char'", StringComparison.Ordinal))
                    return new List<Dictionary<string, object?>>
                    {
                        Fila("COLUMN_NAME", tablaParam.Equals("Ventas", StringComparison.OrdinalIgnoreCase) ? "CanalVenta" : "Marca")
                    };
                if (sql.Contains("SELECT DISTINCT", StringComparison.OrdinalIgnoreCase))
                {
                    if (sql.Contains("[Marca]", StringComparison.Ordinal))
                        return new List<Dictionary<string, object?>> { Fila("V", "Dell"), Fila("V", "HP") };
                    if (sql.Contains("[CanalVenta]", StringComparison.Ordinal))
                        return new List<Dictionary<string, object?>> { Fila("V", "Tienda Física") };
                    return new List<Dictionary<string, object?>>();
                }
                if (sql.Contains("INFORMATION_SCHEMA.COLUMNS", StringComparison.Ordinal))
                    return new List<Dictionary<string, object?>>
                    {
                        Fila("COLUMN_NAME", "Id"), Fila("COLUMN_NAME", "Marca"), Fila("COLUMN_NAME", "Precio")
                    };
                return new List<Dictionary<string, object?>>
                {
                    Fila("Id", "1", "Marca", "Dell", "Precio", "4500.00")
                };
            });

        return new SqlQueryTool(conexionRepo.Object,
            new Mock<ITablaAutorizadaRepository>().Object,
            new Mock<IVistaAutorizadaRepository>().Object,
            cifrador.Object, executor.Object,
            new Mock<Microsoft.Extensions.Logging.ILogger<SqlQueryTool>>().Object);
    }

    [Theory]
    [InlineData(2027, true)]
    [InlineData(1999, true)]
    [InlineData(2100, true)]
    [InlineData(1000, false)]
    [InlineData(1899, false)]
    [InlineData(2101, false)]
    [InlineData(4500.50, false)]
    public void EsAnio_DetectaAniosPorEstructura(decimal valor, bool esperado)
        => Assert.Equal(esperado, SqlQueryTool.EsAnio(valor));

    [Fact]
    public void ElegirUmbral_AnioSueltoSinSimbolo_NoEsUmbral()
    {
        // Plan #9152: "Metas para 2027" no puede producir CostoUnitario > 2027.
        Assert.Null(SqlQueryTool.ElegirUmbral(
            new List<decimal> { 2027 }, "dime las Metas para 2027"));
    }

    [Fact]
    public void ElegirUmbral_AnioSuelto_Usa_El_Siguiente_Numero()
    {
        Assert.Equal(1000, SqlQueryTool.ElegirUmbral(
            new List<decimal> { 2027, 1000 }, "metas 2027 y precio mayor a 1000"));
    }

    [Fact]
    public void ElegirUmbral_ConSimbolo_Respeta_El_Anio()
    {
        Assert.Equal(2027, SqlQueryTool.ElegirUmbral(
            new List<decimal> { 2027 }, "precio > 2027"));
    }

    [Fact]
    public void ElegirUmbral_NumeroNormal_EsUmbral()
    {
        Assert.Equal(1000, SqlQueryTool.ElegirUmbral(
            new List<decimal> { 1000 }, "precio mayor a 1000"));
    }

    [Fact]
    public async Task ActivosConUmbral_NoFiltraPorEstado_SoloPrecioMayor()
    {
        var sqls = new List<string>();
        var tool = CrearToolActivos(sqls);

        var res = await tool.ExecuteAsync(new ToolExecutionRequest
        {
            HerramientaCodigo = "SqlQueryTool",
            Parametros = new Dictionary<string, object?>
            {
                ["pregunta"] = "segun el archivo de ejemplo, cuales son los Objetos de Diccionario, y de cuantos activos el precio supera los 1000?",
                ["modo"] = "CONSULTA"
            }
        }, CancellationToken.None);

        Assert.True(res.Exitoso, res.Error);
        var principal = sqls.First(s => s.Contains("TOP 10", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("[Precio] > 1000", principal);
        Assert.DoesNotContain("[Estado]", principal);
    }

    [Fact]
    public async Task ActivosConUmbral_ContenidoMuestraTotalYFiltro()
    {
        var sqls = new List<string>();
        var tool = CrearToolActivos(sqls);

        var res = await tool.ExecuteAsync(new ToolExecutionRequest
        {
            HerramientaCodigo = "SqlQueryTool",
            Parametros = new Dictionary<string, object?>
            {
                ["pregunta"] = "de cuantos activos el precio supera los 1000?",
                ["modo"] = "CONSULTA"
            }
        }, CancellationToken.None);

        Assert.True(res.Exitoso, res.Error);
        Assert.Contains("Total filtrado: 16", res.Contenido);
        Assert.Contains("Filtro aplicado: WHERE [Precio] > 1000", res.Contenido);
    }
}
