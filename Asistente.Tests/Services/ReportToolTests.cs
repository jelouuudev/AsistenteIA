using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Asistente.Application.Orchestrator;
using Asistente.Application.Services.Herramientas;
using Asistente.Domain.Interfaces;
using Asistente.Shared;
using Moq;
using Xunit;

namespace Asistente.Tests.Services;

/// <summary>
/// ReportTool no debe generar un PDF "exitoso" cuando el paso previo no trajo
/// información real (RAG vacío o SQL sin filas). Antes envolvía el texto marcador
/// en un reporte válido ("Reporte: X / No se encontró...").
///
public class ReportToolTests
{
    private static ReportTool CrearTool()
    {
        var storage = new Mock<IFileStorageService>();
        storage.Setup(s => s.SaveFileAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("reportes/reporte_test.pdf");
        return new ReportTool(storage.Object);
    }

    private static ToolExecutionRequest Request(string titulo, string datos) => new()
    {
        HerramientaCodigo = "ReportTool",
        Parametros = new Dictionary<string, object?> { ["titulo"] = titulo, ["datos"] = datos }
    };

    [Fact]
    public async Task MarcadorDocumental_NoGeneraPdf_YReportaError()
    {
        var tool = CrearTool();

        var res = await tool.ExecuteAsync(
            Request("Reporte: ejemplo", "No se encontró información documental relevante para la consulta."),
            CancellationToken.None);

        Assert.False(res.Exitoso);
        Assert.NotNull(res.Error);
        Assert.DoesNotContain("pdfGenerado", res.Metadatos.Keys);
    }

    [Fact]
    public async Task MarcadorSqlSinFilas_NoGeneraPdf_YReportaError()
    {
        var tool = CrearTool();

        var res = await tool.ExecuteAsync(
            Request("Reporte de Activos", "La consulta no devolvió registros."),
            CancellationToken.None);

        Assert.False(res.Exitoso);
    }

    [Fact]
    public async Task DatosReales_PasanLaValidacion_NoLosRechazaComoVacios()
    {
        var tool = CrearTool();
        var datos = "Datos obtenidos de la tabla 'activos':\nId: 3 | Codigo: ACT-0001 | Nombre: Laptop Dell";

        var res = await tool.ExecuteAsync(
            Request("Reporte de Activos", datos),
            CancellationToken.None);

        // Lo importante del fix: los datos reales NO deben rechazarse con "Sin datos".
        // (La generación del PDF en sí depende del entorno gráfico/QuestPDF del host.)
        Assert.DoesNotContain("Sin datos para el reporte", res.Error ?? string.Empty);
    }

    [Fact]
    public void ContenidoDocumental_NoFabricaTabla_ConservaFuente()
    {
        var datos = "[Fuente: ejemplo]\nPDF es un formato de archivo para representar documentos.\n\n---\n\n[Fuente: ejemplo]\nUn identificador de objetos consta de dos partes.";
        var md = ReportTool.GenerarResumenEjecutivo(datos, "Resumen: ejemplo");

        Assert.StartsWith("# Resumen: ejemplo", md);
        Assert.DoesNotContain("Total de registros", md);
        Assert.Contains("Fuentes consultadas:** 2", md);
        Assert.Contains("[Fuente: ejemplo]", md);
    }

    [Fact]
    public void DatosTabulares_SiguenGenerandoTabla()
    {
        var datos = "Id: 3 | Codigo: ACT-0001 | Nombre: Laptop Dell\nId: 4 | Codigo: ACT-0002 | Nombre: Monitor";
        var md = ReportTool.GenerarResumenEjecutivo(datos, "Reporte de Activos");

        Assert.Contains("Total de registros", md);
        Assert.Contains("Laptop Dell", md);
    }

    [Fact]
    public void ProsaConDosPuntos_NoFabricaTabla()
    {
        var datos = "especificación PDF 1.7, la cabecera debería ser: %PDF-1.7\npalabra clave R. Veamos un ejemplo: 156 0 obj\nsiguientes cuatro elementos: cabecera, cuerpo, tabla y trailer";
        var md = ReportTool.GenerarResumenEjecutivo(datos, "Resumen documental");

        Assert.DoesNotContain("Total de registros", md);
        Assert.StartsWith("# Resumen documental", md);
    }

    [Fact]
    public void RamasParalelas_SumanTotales_YCuentanFilas_NoUnidades()
    {
        // Dos secciones CONSULTA (ramas [1/2] y [2/2]): los totales del header se
        // suman (7+7=14) y los grupos cuentan REGISTROS, no la columna Cantidad
        // (unidades). Regresión del plan 4022 (decía 7 y 357%/300%).
        var datos = "[Paso 1: Consultar datos de Ventas (SQL Server) [1/2]]\n"
            + "Datos obtenidos de la tabla 'ventas' (base de datos 'VentasTest'). Total filtrado: 7. Valor total: 1320.00.\n"
            + "IdVenta: 8 | Producto: Camisa | Categoria: Ropa | Ciudad: Arequipa | Estado: Entregado | Cantidad: 8 | PrecioUnitario: 75.00\n"
            + "IdVenta: 9 | Producto: Jean | Categoria: Ropa | Ciudad: Lima | Estado: Entregado | Cantidad: 3 | PrecioUnitario: 120.00\n"
            + "[Paso 2: Consultar datos de Ventas (SQL Server) [2/2]]\n"
            + "Datos obtenidos de la tabla 'ventas' (base de datos 'VentasTest'). Total filtrado: 7. Valor total: 2660.00.\n"
            + "IdVenta: 15 | Producto: Ollas | Categoria: Hogar | Ciudad: Lima | Estado: Entregado | Cantidad: 2 | PrecioUnitario: 520.00\n"
            + "IdVenta: 16 | Producto: Aspiradora | Categoria: Hogar | Ciudad: Arequipa | Estado: Entregado | Cantidad: 1 | PrecioUnitario: 890.00";
        var md = ReportTool.GenerarResumenEjecutivo(datos, "Reporte Ropa+Hogar");

        Assert.Contains("Total de registros:** 14 (detalle: muestra de 4)", md);
        Assert.Contains("Valor total:**", md);
        Assert.Contains("- Ropa: 2 (14%)", md);
        Assert.Contains("- Hogar: 2 (14%)", md);
        Assert.DoesNotContain("(357%)", md);
        Assert.DoesNotContain("(300%)", md);
    }

    [Fact]
    public void MixtoConDesglose_MuestraValorTotalSql()
    {
        // Plan #9126: el insumo SQL viene en formato desglose (sin "Total:" ni
        // "Valor total:" en el header, pero con "importe total N" calculado en
        // SQL). El informe debe mostrar el Valor total (SQL) exacto.
        var datos = "[Paso 1: Consultar datos de Ventas (SQL Server)]\n"
            + "Datos obtenidos de la tabla 'ventas' (base de datos 'VentasTest'). Desglose por Ciudad (cifras calculadas en SQL, no recalcular). Filtro aplicado: WHERE [Ciudad] = 'Lima' AND [Estado] = 'Entregado'.\n"
            + "- Ciudad = Lima: 6 registros, 19 unidades, importe total 17300.00\n"
            + "[Paso 2: Consultar documentación mediante RAG]\n"
            + "Según **v** (v):\n\nPolitica de Vacaciones 2026\n\nBono Vacacional\n\n- Monto: 300 USD por periodo vacacional";
        var md = ReportTool.GenerarResumenEjecutivo(datos, "Informe mixto");

        Assert.Contains("Valor total (SQL):**", md);
        Assert.Contains("17,300.00", md);
    }

    [Fact]
    public void MixtoSqlMasDocumental_FusionaTotalesYContexto()
    {
        // Plan #9062: el insumo trae totales SQL + prosa RAG. Debe fusionar en dos
        // secciones, sin fabricar tablas de la prosa ni ocultar el total SQL.
        var datos = "[Paso 1: Consultar datos de Activos (SQL Server)]\n"
            + "Datos obtenidos de la tabla 'activos' (base de datos 'ControlActivosTest'). Total filtrado: 16. Filtro aplicado: WHERE [Precio] > 1000.\n"
            + "IdActivo: 1 | Nombre: Laptop Dell | Estado: ACTIVO | Precio: 4500.00\n"
            + "[Fuente: ejemplo]\n"
            + "Según **ejemplo**: un Objeto Diccionario es una tabla asociativa con pares Clave - Valor.";
        var md = ReportTool.GenerarResumenEjecutivo(datos, "Informe mixto");

        Assert.Contains("Total de registros (SQL):** 16", md);
        Assert.Contains("## Contexto documental", md);
        Assert.Contains("[Fuente: ejemplo]", md);
        Assert.DoesNotContain("Total de registros:** 1 (detalle", md);
    }
}

/// <summary>
/// Plan #12197: con varias capas SQL, la entrega mostraba "Total de registros (SQL):
/// 24 / Valor total 33.050" (solo 'ventas', la ultima capa) como si fuera el total
/// general. Ahora se emite un desglose por tabla, leido de las lineas
/// autocontenidas que produce SqlQueryTool. Sin vocabulario de dominio.
/// </summary>
public class ReportToolTotalesPorTablaTests
{
    [Fact]
    public void VariasTablas_DevuelveTotalesPorTabla_SinGranTotal()
    {
        var datos = string.Join("\n", new[]
        {
            "Datos obtenidos de la tabla 'insumos' (base de datos 'ComidaTest'). Total: 8 (343 unidades, importe total 2723.50).",
            "Datos obtenidos de la tabla 'pedidos' (base de datos 'ComidaTest'). Total: 12 (28 unidades, importe total 3276.00).",
            "Datos obtenidos de la tabla 'ventas' (base de datos 'VentasTest'). Total: 24 (96 unidades, importe total 33050.00).",
            "Segun **ejemplo** (ejemplo): el cuerpo del archivo contiene los objetos indirectos."
        });

        var salida = ReportTool.GenerarResumenEjecutivo(datos, "Informe");

        Assert.Contains("Totales por tabla", salida);
        Assert.Contains("ComidaTest.insumos", salida);
        Assert.Contains("ComidaTest.pedidos", salida);
        Assert.Contains("VentasTest.ventas", salida);
        Assert.Contains("8 registros", salida);
        Assert.Contains("33,050.00", salida);
        // El total enganoso (una sola capa como si fuera todo) ya no aparece.
        Assert.DoesNotContain("**Total de registros (SQL):** 24", salida);
    }

    [Fact]
    public void UnaSolaTabla_ConservaElGranTotal()
    {
        // No se rompe el caso legitimo: dos ramas paralelas de la MISMA tabla se
        // suman en un unico total.
        var datos = string.Join("\n", new[]
        {
            "[Paso 1: Consultar datos de Activos]",
            "Datos obtenidos de la tabla 'activos' (base de datos 'ControlActivosTest'): Total filtrado: 2. Valor total: 9000.00.",
            "[Paso 2: Consultar datos de Activos]",
            "Datos obtenidos de la tabla 'activos' (base de datos 'ControlActivosTest'): Total filtrado: 3. Valor total: 7000.00."
        });

        var salida = ReportTool.GenerarResumenEjecutivo(datos, "Informe");

        Assert.Contains("**Total de registros (SQL):** 5", salida);
        Assert.Contains("**Valor total (SQL):** 16,000.00", salida);
        Assert.DoesNotContain("Totales por tabla", salida);
    }

    [Fact]
    public void RecorteDeContexto_CaeEnSaltoDeLinea_NoCortaAPalabra()
    {
        // El corte por la cola mutilaba la primera palabra ("lumna decimal
        // 631650.00)"); ahora cae en un salto de linea.
        var contexto = string.Join("\n", Enumerable.Range(1, 200)
            .Select(i => $"linea {i} de contexto suficientemente larga para el recorte"));

        var recorte = AgentOrchestrator.RecortarContexto(contexto, 400);

        Assert.NotNull(recorte);
        Assert.StartsWith("[contexto recortado: se conserva lo más reciente]\n", recorte);
        var cuerpo = recorte!["[contexto recortado: se conserva lo más reciente]\n".Length..];
        Assert.StartsWith("linea ", cuerpo);
        Assert.Contains("linea 200 de contexto", cuerpo);
    }

    /// <summary>
    /// El resumen MIXTO (SQL + RAG) debe incluir las filas de SQL. No lo hacIa:
    /// escribia totales y prosa documental y se comia la tabla, asi que el PDF de
    /// aceptacion salia sin los datos que el paso de riesgos debia analizar
    /// (#1012: el clasificador vio solo totales y respondio "sin riesgos").
    /// </summary>
    [Fact]
    public void ResumenMixto_IncluyeLasFilasDeSql()
    {
        // Formato real de SqlQueryTool: "campo: valor | campo: valor" (sin pipes
        // en los extremos). No es markdown; el informe lo convierte a tabla.
        var datos = string.Join("\n", new[]
        {
            "Datos obtenidos de la tabla 'insumos'. Total: 3",
            "Id: 1 | Nombre: Arroz | Stock: 120 | StockMinimo: 50",
            "Id: 2 | Nombre: Aji | Stock: 15 | StockMinimo: 20",
            "Id: 3 | Nombre: Queso | Stock: 8 | StockMinimo: 10",
            "CONTEXTO DOCUMENTAL",
            "Un documento PDF puede contener elementos interactivos. [Fuente: ejemplo]"
        });

        var r = ReportTool.FormatearResumenMixto(datos, "Informe");

        Assert.Contains("## Datos obtenidos", r);
        Assert.Contains("Arroz", r);
        Assert.Contains("Aji", r);
        Assert.Contains("Queso", r);
        Assert.Contains("Contexto documental", r);
    }

    [Fact]
    public void ResumenMixto_SinFilas_NoInventaTabla()
    {
        var datos = "CONTEXTO DOCUMENTAL\nSolo prosa. [Fuente: ejemplo]";
        var r = ReportTool.FormatearResumenMixto(datos, "Informe");

        Assert.DoesNotContain("## Datos obtenidos", r);
    }
}