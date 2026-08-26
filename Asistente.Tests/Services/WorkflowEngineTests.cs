using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Asistente.Application.Services.Workflows;
using Asistente.Domain.Entities;
using Asistente.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Asistente.Tests.Services;

public class WorkflowEngineTests
{
    private readonly Mock<IWorkflowRepository> _workflowRepo = new();
    private readonly Mock<IWorkflowPasoRepository> _pasoRepo = new();
    private readonly Mock<IWorkflowEjecucionRepository> _ejecRepo = new();
    private readonly Mock<IWorkflowPasoEjecucionRepository> _pasoEjecRepo = new();
    private readonly Mock<IConfiguracionWorkflowRepository> _configRepo = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<IToolOrchestrator> _orchestrator = new();
    private readonly List<WorkflowEjecucion> _ejecucionesGuardadas = new();
    private readonly List<WorkflowPasoEjecucion> _pasosGuardados = new();

    private ConfiguracionWorkflow ConfigPorDefecto() => new()
    {
        ReintentosMaximos = 2,
        TiempoMaximoPasoMs = 60000,
        TiempoMaximoFlujoMs = 300000,
        ConfirmacionesObligatorias = true,
        LimitePasosPorWorkflow = 10
    };

    private WorkflowEngine CrearEngine(ConfiguracionWorkflow? config = null)
    {
        _uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _configRepo.Setup(r => r.GetAsync()).ReturnsAsync(config ?? ConfigPorDefecto());

        _ejecRepo.Setup(r => r.AddAsync(It.IsAny<WorkflowEjecucion>(), It.IsAny<CancellationToken>()))
            .Callback<WorkflowEjecucion, CancellationToken>((e, _) => { e.IdEjecucion = _ejecucionesGuardadas.Count + 1; _ejecucionesGuardadas.Add(e); })
            .Returns(Task.CompletedTask);
        _ejecRepo.Setup(r => r.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int id, CancellationToken _) => _ejecucionesGuardadas.FirstOrDefault(x => x.IdEjecucion == id));
        _ejecRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(_ejecucionesGuardadas);
        _pasoEjecRepo.Setup(r => r.AddAsync(It.IsAny<WorkflowPasoEjecucion>(), It.IsAny<CancellationToken>()))
            .Callback<WorkflowPasoEjecucion, CancellationToken>((p, _) => { p.IdPasoEjecucion = _pasosGuardados.Count + 1; _pasosGuardados.Add(p); })
            .Returns(Task.CompletedTask);
        _pasoEjecRepo.Setup(r => r.GetByEjecucionAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkflowPasoEjecucion>());

        return new WorkflowEngine(_workflowRepo.Object, _ejecRepo.Object,
            _pasoEjecRepo.Object, _configRepo.Object, _orchestrator.Object,
            _uow.Object, new Mock<ILogger<WorkflowEngine>>().Object);
    }

    private Workflow ConstruirWorkflow(List<WorkflowPaso> pasos)
    {
        var wf = new Workflow
        {
            IdWorkflow = 1,
            Nombre = "Reporte Clientes",
            Codigo = "REP-CLI",
            Estado = EstadoWorkflow.Activo,
            Pasos = pasos
        };
        _workflowRepo.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(wf);
        _pasoRepo.Setup(r => r.GetByWorkflowAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(pasos);
        return wf;
    }

    private static WorkflowPaso Paso(int orden, string herramienta, bool requiereConf = false, string parametros = "{}") => new()
    {
        IdPaso = orden,
        IdWorkflow = 1,
        Orden = orden,
        Nombre = herramienta,
        Herramienta = herramienta,
        Parametros = parametros,
        RequiereConfirmacion = requiereConf,
        ReintentosMaximos = 2,
        TiempoMaximoMs = 60000,
        EstrategiaError = EstrategiaError.Cancelar
    };

    [Fact]
    public async Task Caso1_TresPasos_EjecutanEnOrdenYResultadoPresentado()
    {
        var pasos = new List<WorkflowPaso> { Paso(1, "SqlQueryTool"), Paso(2, "ReportTool"), Paso(3, "DateTimeTool") };
        ConstruirWorkflow(pasos);
        var orden = new List<string>();
        _orchestrator.Setup(o => o.EjecutarAsync(It.IsAny<ToolExecutionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ToolExecutionRequest r, CancellationToken _) =>
            {
                orden.Add(r.HerramientaCodigo);
                return new ToolExecutionResult { Exitoso = true, Contenido = $"ok-{r.HerramientaCodigo}" };
            });

        var engine = CrearEngine();
        var res = await engine.EjecutarAsync(1, 1, 1, false, null, CancellationToken.None);

        Assert.Equal("Exitosa", res.Estado);
        Assert.Equal(3, res.Pasos.Count);
        Assert.Equal(new[] { "SqlQueryTool", "ReportTool", "DateTimeTool" }, orden);
        Assert.Contains("ok-DateTimeTool", res.ResultadoFinal);
    }

    [Fact]
    public async Task Caso2_PasoFallaConReintentos_ReintentaAntesDeMarcarError()
    {
        var pasos = new List<WorkflowPaso> { Paso(1, "SqlQueryTool"), Paso(2, "ReportTool"), Paso(3, "DateTimeTool") };
        ConstruirWorkflow(pasos);
        int intentos = 0;
        _orchestrator.Setup(o => o.EjecutarAsync(It.IsAny<ToolExecutionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ToolExecutionRequest r, CancellationToken _) =>
            {
                if (r.HerramientaCodigo == "ReportTool")
                {
                    intentos++;
                    return new ToolExecutionResult { Exitoso = false, Error = "fallo simulado" };
                }
                return new ToolExecutionResult { Exitoso = true, Contenido = "ok" };
            });

        var engine = CrearEngine();
        var res = await engine.EjecutarAsync(1, 1, 1, false, null, CancellationToken.None);

        Assert.Equal("Error", res.Estado);
        Assert.Equal(3, intentos); // 1 intento inicial + 2 reintentos configurados
    }

    [Fact]
    public async Task Caso4_Historial_RegistraCadaPasoConEstado()
    {
        var pasos = new List<WorkflowPaso> { Paso(1, "SqlQueryTool"), Paso(2, "ReportTool"), Paso(3, "DateTimeTool") };
        ConstruirWorkflow(pasos);
        _orchestrator.Setup(o => o.EjecutarAsync(It.IsAny<ToolExecutionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ToolExecutionResult { Exitoso = true, Contenido = "ok" });

        var engine = CrearEngine();
        await engine.EjecutarAsync(1, 1, 1, false, null, CancellationToken.None);

        Assert.Equal(3, _pasosGuardados.Count);
        Assert.All(_pasosGuardados, p => Assert.Equal("Exitosa", p.Estado));
        Assert.Equal(1, _ejecucionesGuardadas.Count);
    }

    [Fact]
    public async Task ContextoCompartido_Paso2RecibeResultadoPaso1MedianteToken()
    {
        var pasos = new List<WorkflowPaso>
        {
            Paso(1, "SqlQueryTool"),
            Paso(2, "ReportTool", parametros: "{\"contenido\":\"{{resultado}}\"}"),
            Paso(3, "DateTimeTool")
        };
        ConstruirWorkflow(pasos);
        string? parametroVistoEnPaso2 = null;
        _orchestrator.Setup(o => o.EjecutarAsync(It.IsAny<ToolExecutionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ToolExecutionRequest r, CancellationToken _) =>
            {
                if (r.HerramientaCodigo == "SqlQueryTool")
                    return new ToolExecutionResult { Exitoso = true, Contenido = "clientes:5" };
                if (r.HerramientaCodigo == "ReportTool")
                {
                    r.Parametros.TryGetValue("contenido", out var v);
                    parametroVistoEnPaso2 = v?.ToString();
                    return new ToolExecutionResult { Exitoso = true, Contenido = "reporte" };
                }
                return new ToolExecutionResult { Exitoso = true, Contenido = "fecha" };
            });

        var engine = CrearEngine();
        await engine.EjecutarAsync(1, 1, 1, false, null, CancellationToken.None);

        Assert.NotNull(parametroVistoEnPaso2);
        Assert.Contains("clientes:5", parametroVistoEnPaso2!);
    }

    [Fact]
    public async Task Confirmacion_RequiereConfirmacionYNoEjecutaPasoSensible()
    {
        var pasos = new List<WorkflowPaso> { Paso(1, "SqlQueryTool"), Paso(2, "ReportTool", requiereConf: true), Paso(3, "DateTimeTool") };
        ConstruirWorkflow(pasos);
        bool pasoSensibleEjecutado = false;
        _orchestrator.Setup(o => o.EjecutarAsync(It.IsAny<ToolExecutionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ToolExecutionRequest r, CancellationToken _) =>
            {
                if (r.HerramientaCodigo == "ReportTool") pasoSensibleEjecutado = true;
                return new ToolExecutionResult { Exitoso = true, Contenido = "ok" };
            });

        var engine = CrearEngine();
        var res = await engine.EjecutarAsync(1, 1, 1, confirmado: false, null, CancellationToken.None);

        Assert.True(res.RequiereConfirmacion);
        Assert.False(pasoSensibleEjecutado);
    }
}
