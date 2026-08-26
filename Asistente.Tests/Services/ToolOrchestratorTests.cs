using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Asistente.Application.Services;
using Asistente.Application.Services.Herramientas;
using Asistente.Application.Interfaces;
using Asistente.Domain.Entities;
using Asistente.Domain.Interfaces;
using Asistente.Shared;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Asistente.Tests.Services;

public class CalculatorToolTests
{
    private readonly CalculatorTool _tool = new();

    [Theory]
    [InlineData("2 + 3 * 4", 14)]
    [InlineData("125 * 8 / 5", 200)]
    [InlineData("(10 + 10) / 4", 5)]
    public async Task CalculaExpresionesBasicas(string expr, double esperado)
    {
        var req = new ToolExecutionRequest { HerramientaCodigo = "CalculatorTool", Parametros = new() { ["expresion"] = expr } };
        var res = await _tool.ExecuteAsync(req);
        Assert.True(res.Exitoso);
        Assert.Contains(esperado.ToString(), res.Contenido);
    }

    [Fact]
    public async Task RechazaExpresionInvalida()
    {
        var req = new ToolExecutionRequest { HerramientaCodigo = "CalculatorTool", Parametros = new() { ["expresion"] = "2 +" } };
        var res = await _tool.ExecuteAsync(req);
        Assert.False(res.Exitoso);
    }

    [Fact]
    public async Task RechazaIntentoDeAccesoNoPermitido()
    {
        // No debe permitir llamadas a métodos del sistema
        var req = new ToolExecutionRequest { HerramientaCodigo = "CalculatorTool", Parametros = new() { ["expresion"] = "System.IO.File.Delete(\"x\")" } };
        var res = await _tool.ExecuteAsync(req);
        Assert.False(res.Exitoso);
    }
}

public class ToolOrchestratorTests
{
    private readonly Mock<IHerramientaRepository> _herramientaRepo = new();
    private readonly Mock<IAsistenteHerramientaRepository> _asocRepo = new();
    private readonly Mock<IEjecucionHerramientaRepository> _ejecRepo = new();
    private readonly Mock<IUsuarioRepository> _usuarioRepo = new();
    private readonly Mock<IConfiguracionOrchestratorRepository> _configRepo = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<ITool> _tool = new();
    private readonly Mock<IAutorizacionService> _autorizacion = new();
    private readonly ToolOrchestrator _orchestrator;

    public ToolOrchestratorTests()
    {
        _uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _ejecRepo.Setup(r => r.AddAsync(It.IsAny<EjecucionHerramienta>())).Returns(Task.CompletedTask);
        _configRepo.Setup(r => r.GetAsync()).ReturnsAsync(new ConfiguracionOrchestrator
        {
            IdConfiguracion = 1,
            Habilitado = true,
            RequiereAutorizacion = true,
            TiempoMaximoEjecucionMs = 30000
        });
        _tool.Setup(t => t.Name).Returns("SqlQueryTool");
        _tool.Setup(t => t.ExecuteAsync(It.IsAny<ToolExecutionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ToolExecutionResult { Exitoso = true, Contenido = "ok" });

        _autorizacion.Setup(a => a.VerificarHerramientaAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResultadoAutorizacion { Permitido = true });

        _orchestrator = new ToolOrchestrator(
            new List<ITool> { _tool.Object },
            _herramientaRepo.Object, _asocRepo.Object, _ejecRepo.Object,
            _usuarioRepo.Object, _configRepo.Object, _uow.Object,
            new Mock<ILogger<ToolOrchestrator>>().Object,
            _autorizacion.Object);
    }

    [Fact]
    public async Task HerramientaInexistente_RechazadaYAuditada()
    {
        _herramientaRepo.Setup(r => r.GetByCodigoAsync("NoExiste")).ReturnsAsync((Herramienta?)null);
        var res = await _orchestrator.EjecutarAsync(new ToolExecutionRequest { HerramientaCodigo = "NoExiste", IdUsuario = 1 });
        Assert.False(res.Exitoso);
        _ejecRepo.Verify(r => r.AddAsync(It.Is<EjecucionHerramienta>(e => e.Estado == "Rechazada")), Times.Once);
    }

    [Fact]
    public async Task HerramientaInactiva_Rechazada()
    {
        _herramientaRepo.Setup(r => r.GetByCodigoAsync("SqlQueryTool"))
            .ReturnsAsync(new Herramienta { IdHerramienta = 2, Codigo = "SqlQueryTool", Activa = false, RequierePermiso = true });
        var res = await _orchestrator.EjecutarAsync(new ToolExecutionRequest { HerramientaCodigo = "SqlQueryTool", IdUsuario = 1, IdAsistente = 1 });
        Assert.False(res.Exitoso);
    }

    [Fact]
    public async Task SinAsociacionAlAsistente_RechazadaPorPermiso()
    {
        _herramientaRepo.Setup(r => r.GetByCodigoAsync("SqlQueryTool"))
            .ReturnsAsync(new Herramienta { IdHerramienta = 2, Codigo = "SqlQueryTool", Activa = true, RequierePermiso = true });
        // No asociada -> devuelve null
        _asocRepo.Setup(r => r.GetAsync(1, 2)).ReturnsAsync((AsistenteHerramienta?)null);
        var res = await _orchestrator.EjecutarAsync(new ToolExecutionRequest { HerramientaCodigo = "SqlQueryTool", IdUsuario = 1, IdAsistente = 1 });
        Assert.False(res.Exitoso);
        Assert.Contains("autorizada", res.Error ?? "");
    }

    [Fact]
    public async Task AsociadaYActiva_EjecutaYAuditaExito()
    {
        _herramientaRepo.Setup(r => r.GetByCodigoAsync("SqlQueryTool"))
            .ReturnsAsync(new Herramienta { IdHerramienta = 2, Codigo = "SqlQueryTool", Activa = true, RequierePermiso = true });
        _asocRepo.Setup(r => r.GetAsync(1, 2)).ReturnsAsync(new AsistenteHerramienta { IdAsistente = 1, IdHerramienta = 2, Activa = true });
        var res = await _orchestrator.EjecutarAsync(new ToolExecutionRequest { HerramientaCodigo = "SqlQueryTool", IdUsuario = 1, IdAsistente = 1, Parametros = new() });
        Assert.True(res.Exitoso);
        _tool.Verify(t => t.ExecuteAsync(It.IsAny<ToolExecutionRequest>(), It.IsAny<CancellationToken>()), Times.Once);
        _ejecRepo.Verify(r => r.AddAsync(It.Is<EjecucionHerramienta>(e => e.Estado == "Exitosa")), Times.Once);
    }
}
