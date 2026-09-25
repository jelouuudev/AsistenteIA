using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Asistente.Application.Interfaces;
using Asistente.Application.Services.Herramientas;
using Asistente.Domain.Interfaces;
using Asistente.Shared;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Asistente.Tests.Services;

/// <summary>
/// DocumentSearchTool debe pasar la preferencia de documento a RAG para que la
/// búsqueda dirigida traiga fragmentos DE ESE documento (resumen al subir).
///
public class DocumentSearchToolTests
{
    [Fact]
    public async Task ParametroDocumento_SePasaComoPreferenciaARag()
    {
        var rag = new Mock<IRagService>();
        string? preferenciaVisto = null;
        rag.Setup(r => r.RecuperarContextoDocumentalAsync(
                It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<string?>()))
            .Callback<string, int?, string?>((_, _, pref) => preferenciaVisto = pref)
            .ReturnsAsync(new RagContextoDto
            {
                ContextoDocumental = "Contenido del documento.",
                TotalFragmentos = 1
            });

        var tool = new DocumentSearchTool(rag.Object, new Mock<ILogger<DocumentSearchTool>>().Object);
        var res = await tool.ExecuteAsync(new ToolExecutionRequest
        {
            HerramientaCodigo = "DocumentSearchTool",
            Parametros = new Dictionary<string, object?>
            {
                ["consulta"] = "Resumen del contenido",
                ["documento"] = "ejemplo"
            }
        }, CancellationToken.None);

        Assert.True(res.Exitoso);
        Assert.Equal("ejemplo", preferenciaVisto);
    }

    [Fact]
    public async Task SinParametroDocumento_PreferenciaEsNull()
    {
        var rag = new Mock<IRagService>();
        string? preferenciaVisto = "no-llamado";
        rag.Setup(r => r.RecuperarContextoDocumentalAsync(
                It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<string?>()))
            .Callback<string, int?, string?>((_, _, pref) => preferenciaVisto = pref)
            .ReturnsAsync(new RagContextoDto { ContextoDocumental = "x", TotalFragmentos = 1 });

        var tool = new DocumentSearchTool(rag.Object, new Mock<ILogger<DocumentSearchTool>>().Object);
        await tool.ExecuteAsync(new ToolExecutionRequest
        {
            HerramientaCodigo = "DocumentSearchTool",
            Parametros = new Dictionary<string, object?> { ["consulta"] = "algo" }
        }, CancellationToken.None);

        Assert.Null(preferenciaVisto);
    }

    [Fact]
    public async Task RagVacio_DevuelveMarcadorSinDatos()
    {
        var rag = new Mock<IRagService>();
        rag.Setup(r => r.RecuperarContextoDocumentalAsync(
                It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<string?>()))
            .ReturnsAsync(new RagContextoDto { ContextoDocumental = string.Empty, TotalFragmentos = 0 });

        var tool = new DocumentSearchTool(rag.Object, new Mock<ILogger<DocumentSearchTool>>().Object);
        var res = await tool.ExecuteAsync(new ToolExecutionRequest
        {
            HerramientaCodigo = "DocumentSearchTool",
            Parametros = new Dictionary<string, object?> { ["consulta"] = "ejemplo" }
        }, CancellationToken.None);

        Assert.True(res.Exitoso);
        Assert.Contains("No se encontró información documental", res.Contenido);
    }
}
