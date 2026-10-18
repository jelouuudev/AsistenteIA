using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Asistente.Application.Interfaces;
using Asistente.Application.Services.Workflows;
using Asistente.Domain.Entities;
using Asistente.Domain.Interfaces;
using Asistente.Shared;
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
    private Mock<Asistente.Application.Services.Memoria.MemoryOrchestrator>? _memoriaMock;

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

        var authMock = new Mock<IAutorizacionService>();
        authMock.Setup(a => a.VerificarWorkflowAsistenteAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResultadoAutorizacion { Permitido = true });

        // Memoria del workflow: sin estado previo ni bloque de conocimiento.
        var memoriaMock = new Mock<Asistente.Application.Services.Memoria.MemoryOrchestrator>(
            null!, null!, null!, new Mock<ILogger<Asistente.Application.Services.Memoria.MemoryOrchestrator>>().Object);
        memoriaMock
            .Setup(m => m.ObtenerContextoAsync(It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<int?>(),
                It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(string.Empty);
        memoriaMock
            .Setup(m => m.TryLeerEstadoWorkflowAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Asistente.Shared.MemoryDto?)null);
        memoriaMock
            .Setup(m => m.GuardarEstadoWorkflowAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Asistente.Shared.MemoryDto());
        _memoriaMock = memoriaMock;

        return new WorkflowEngine(_workflowRepo.Object, _ejecRepo.Object,
            _pasoEjecRepo.Object, _configRepo.Object, _orchestrator.Object,
            _uow.Object, authMock.Object, new Mock<ILogger<WorkflowEngine>>().Object,
            memoriaMock.Object);
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

    [Fact]
    public async Task ContextoInicial_SustituyePlaceholdersDeEventoEnParametros()
    {
        var pasos = new List<WorkflowPaso>
        {
            Paso(1, "DocumentSearchTool", parametros: "{\"consulta\": \"{{Nombre}}\"}")
        };
        ConstruirWorkflow(pasos);
        Dictionary<string, object?>? capturados = null;
        _orchestrator.Setup(o => o.EjecutarAsync(It.IsAny<ToolExecutionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ToolExecutionRequest r, CancellationToken _) =>
            {
                capturados = r.Parametros;
                return new ToolExecutionResult { Exitoso = true, Contenido = "ok" };
            });

        var engine = CrearEngine();
        var ctx = new Dictionary<string, string> { ["Nombre"] = "Manual de Producto", ["Codigo"] = "MAN-001" };
        var res = await engine.EjecutarAsync(1, 1, null, true, null, CancellationToken.None, contextoInicial: ctx);

        Assert.True(res.Exitoso);
        Assert.NotNull(capturados);
        Assert.Equal("Manual de Producto", capturados!["consulta"]?.ToString());
    }

    /// <summary>
    /// La memoria del ámbito Workflow entra al contexto compartido: un paso puede
    /// referenciarla con {{memoria}}. Antes el motor solo daba soporte a
    /// {{resultado}}/{{PasoN}}/{{contextoInicial}} y la memoria era inalcanzable
    /// desde una definición de flujo.
    /// </summary>
    [Fact]
    public async Task MemoriaWorkflow_InyectaElBloqueEnLosParametrosDelPaso()
    {
        var pasos = new List<WorkflowPaso>
        {
            Paso(1, "ReportTool", parametros: "{\"titulo\":\"Formato: {{memoria}}\"}")
        };
        ConstruirWorkflow(pasos);
        string? tituloVisto = null;
        _orchestrator.Setup(o => o.EjecutarAsync(It.IsAny<ToolExecutionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ToolExecutionRequest r, CancellationToken _) =>
            {
                tituloVisto = r.Parametros["titulo"]?.ToString();
                return new ToolExecutionResult { Exitoso = true, Contenido = "reporte" };
            });
        var engine = CrearEngine();
        _memoriaMock!
            .Setup(m => m.ObtenerContextoAsync(It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<int?>(),
                It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("[Memoria Organizacion: Formato]\ncolumnas: codigo, nombre");

        await engine.EjecutarAsync(1, 7, 3, true, null, CancellationToken.None);

        Assert.NotNull(tituloVisto);
        Assert.Contains("columnas: codigo, nombre", tituloVisto!);
    }

    /// <summary>
    /// La ejecución arranca con el estado que el propio flujo guardó antes, para
    /// poder continuar tras un reinicio o una reanudación por confirmación.
    /// </summary>
    [Fact]
    public async Task MemoriaWorkflow_RetomaElEstadoPrevioGuardado()
    {
        var pasos = new List<WorkflowPaso>
        {
            Paso(1, "ReportTool", parametros: "{\"contenido\":\"{{estadoPrevio}}\"}")
        };
        ConstruirWorkflow(pasos);
        string? contenidoVisto = null;
        _orchestrator.Setup(o => o.EjecutarAsync(It.IsAny<ToolExecutionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ToolExecutionRequest r, CancellationToken _) =>
            {
                contenidoVisto = r.Parametros["contenido"]?.ToString();
                return new ToolExecutionResult { Exitoso = true, Contenido = "reporte" };
            });
        var engine = CrearEngine();
        _memoriaMock!
            .Setup(m => m.TryLeerEstadoWorkflowAsync("WF-ESTADO-1", It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Asistente.Shared.MemoryDto
            {
                Codigo = "WF-ESTADO-1",
                Contenido = "Paso 1 (SqlQueryTool): clientes:5",
                VersionActual = 3
            });

        await engine.EjecutarAsync(1, 7, 3, true, null, CancellationToken.None);

        Assert.NotNull(contenidoVisto);
        Assert.Contains("clientes:5", contenidoVisto!);
    }

    /// <summary>
    /// Al cerrar el flujo, su estado queda en memoria de ámbito Workflow (una versión
    /// nueva por ejecución), que es lo que hace posible la reanudación del caso anterior.
    /// </summary>
    [Fact]
    public async Task MemoriaWorkflow_PersisteElEstadoAlTerminarElFlujo()
    {
        var pasos = new List<WorkflowPaso> { Paso(1, "SqlQueryTool") };
        ConstruirWorkflow(pasos);
        _orchestrator.Setup(o => o.EjecutarAsync(It.IsAny<ToolExecutionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ToolExecutionResult { Exitoso = true, Contenido = "clientes:5" });
        var engine = CrearEngine();

        var res = await engine.EjecutarAsync(1, 7, 3, true, null, CancellationToken.None);

        Assert.True(res.Exitoso);
        _memoriaMock!.Verify(m => m.GuardarEstadoWorkflowAsync(
            1, "WF-ESTADO-1", It.IsAny<string>(), It.IsAny<string>(), 7, It.IsAny<CancellationToken>()),
            Times.Once);
        _memoriaMock.Verify(m => m.GuardarEstadoWorkflowAsync(
            1, "WF-ESTADO-1", It.IsAny<string>(),
            It.Is<string>(c => c.Contains("clientes:5")), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Un fallo al guardar la memoria no puede dar por fallida una ejecución que ya
    /// se cerró correctamente en la base de datos.
    /// </summary>
    [Fact]
    public async Task MemoriaWorkflow_SiFalla_NoTumbaLaEjecucion()
    {
        var pasos = new List<WorkflowPaso> { Paso(1, "SqlQueryTool") };
        ConstruirWorkflow(pasos);
        _orchestrator.Setup(o => o.EjecutarAsync(It.IsAny<ToolExecutionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ToolExecutionResult { Exitoso = true, Contenido = "ok" });
        var engine = CrearEngine();
        _memoriaMock!
            .Setup(m => m.ObtenerContextoAsync(It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<int?>(),
                It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("vector store caído"));
        _memoriaMock
            .Setup(m => m.GuardarEstadoWorkflowAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("vector store caído"));

        var res = await engine.EjecutarAsync(1, 7, 3, true, null, CancellationToken.None);

        Assert.True(res.Exitoso);
        Assert.Equal("Exitosa", res.Estado);
    }
}
