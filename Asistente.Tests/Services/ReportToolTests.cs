using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
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
}
