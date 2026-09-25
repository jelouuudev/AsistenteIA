using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Asistente.Application.Interfaces;
using Asistente.Application.Orchestrator.Planner;
using Asistente.Application.Services.Herramientas;
using Asistente.Domain.Entities;
using Asistente.Domain.Interfaces;
using Asistente.Shared;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Asistente.Tests.Services;

/// <summary>
/// Pruebas unitarias del Planner Engine (ETAPA 18): Plan Builder, Plan Validator,
/// Execution Graph Builder y Execution Supervisor. Aíslan la lógica de score/validación
/// de la BD y del LLM (mock de repos + Ollama).
/// </summary>
public class PlannerTests
{
    private static Asistente.Domain.Entities.Asistente Agente(string codigo, int id, params string[] herramientas)
        => new()
        {
            IdAsistente = id,
            Codigo = codigo,
            Nombre = codigo,
            Activo = true,
            AsistentesHerramientas = herramientas.Select(h => new AsistenteHerramienta
            {
                Activa = true,
                Herramienta = new Herramienta { Codigo = h, Nombre = h }
            }).ToList()
        };

    private static Mock<IAsistenteRepository> RepoAsistentes(params Asistente.Domain.Entities.Asistente[] agentes)
    {
        var repo = new Mock<IAsistenteRepository>();
        repo.Setup(r => r.GetAllAsync())
            .ReturnsAsync(agentes.ToList());
        repo.Setup(r => r.GetByIdAsync(It.IsAny<int>()))
            .ReturnsAsync((int id) => agentes.FirstOrDefault(a => a.IdAsistente == id));
        repo.Setup(r => r.GetHerramientasActivasAsync(It.IsAny<int>()))
            .ReturnsAsync((int id) => agentes.First(a => a.IdAsistente == id)
                .AsistentesHerramientas.Where(h => h.Activa).Select(h => h.Herramienta.Codigo).ToList());
        return repo;
    }

    private static Mock<IUsuarioRepository> RepoUsuario(Usuario? usuario = null)
    {
        var repo = new Mock<IUsuarioRepository>();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<int>()))
            .ReturnsAsync(usuario);
        return repo;
    }

    private static Mock<IPoliticaIARepository> RepoPolitica()
    {
        var repo = new Mock<IPoliticaIARepository>();
        repo.Setup(r => r.GetByTipoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PoliticaIA?)null);
        return repo;
    }

    private static Mock<IWorkflowRepository> RepoWorkflow()
    {
        var repo = new Mock<IWorkflowRepository>();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Workflow?)null);
        repo.Setup(r => r.GetActivosAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Workflow>());
        return repo;
    }

    private static Mock<IConexionBaseDatosRepository> RepoConexion()
    {
        var repo = new Mock<IConexionBaseDatosRepository>();
        return repo;
    }

    private static Mock<IOllamaService> OllamaStub()
    {
        var mock = new Mock<IOllamaService>();
        mock.Setup(o => o.SendMessageAsync(It.IsAny<IEnumerable<Mensaje>>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<double?>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("respuesta simulada");
        mock.Setup(o => o.IsDisponibleAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        return mock;
    }

    private static Usuario UsuarioAdmin()
        => new()
        {
            IdUsuario = 1,
            UsuarioNombre = "admin",
            Nombres = "Admin",
            Apellidos = "Test",
            Activo = true,
            UsuarioRoles = new List<UsuarioRol>
            {
                new() { IdUsuario = 1, IdRol = 1, Rol = new Rol { IdRol = 1, Nombre = "Administrador" } }
            }
        };

    private static Mock<IAutorizacionService> AuthStub(bool permitido = true)
    {
        var mock = new Mock<IAutorizacionService>();
        mock.Setup(a => a.VerificarHerramientaAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResultadoAutorizacion { Permitido = permitido });
        mock.Setup(a => a.VerificarAsistenteAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResultadoAutorizacion { Permitido = permitido });
        return mock;
    }

    private static Mock<ILogger<T>> Logger<T>() where T : class => new();

    [Fact]
    public async Task PlanBuilder_DeteccionPorIntencion_CreaPasosEsperados()
    {
        var agentes = new[]
        {
            Agente("COMERCIAL-01", 1008, "SqlQueryTool", "ReportTool"),
            Agente("SOPORTE-01", 2005, "DocumentSearchTool"),
            Agente("REPORTES-01", 2006, "ReportTool")
        };
        var builder = new PlanBuilder(
            RepoAsistentes(agentes).Object,
            OllamaStub().Object,
            Logger<PlanBuilder>().Object,
            RepoConexion().Object,
            RepoWorkflow().Object);

        var plan = await builder.ConstruirAsync(
            "Analiza las ventas del trimestre, compara el cumplimiento con el procedimiento documentado, genera un informe ejecutivo en PDF y resalta los riesgos detectados.",
            1, CancellationToken.None);

        // Verificar que crea pasos y tiene la estructura esperada
        Assert.NotEmpty(plan.Pasos);
        Assert.True(plan.Pasos.Count >= 3, "Debe crear al menos 3 pasos");
        Assert.True(plan.Pasos.Any(p => p.Orden == 0), "Debe tener paso de coordinación en orden 0");
        Assert.True(plan.Pasos.Any(p => p.CodigoHerramienta == "ReportTool"), "Debe incluir ReportTool");
        Assert.True(plan.Pasos.Any(p => p.CodigoHerramienta == "DocumentSearchTool"), "Debe incluir DocumentSearchTool");
    }

    [Fact]
    public async Task PlanBuilder_AccionSensible_RequiereAprobacion()
    {
        var agentes = new[] { Agente("COMERCIAL-01", 1008, "SqlQueryTool") };
        var builder = new PlanBuilder(
            RepoAsistentes(agentes).Object,
            OllamaStub().Object,
            Logger<PlanBuilder>().Object,
            RepoConexion().Object,
            RepoWorkflow().Object);

        var plan = await builder.ConstruirAsync("Eliminar los registros de ventas obsoletos", 1, CancellationToken.None);

        Assert.True(plan.RequiereAprobacion);
        Assert.Contains(plan.Pasos, p => p.Tipo == "Approval");
    }

    [Fact]
    public async Task PlanBuilder_ObjetivoConFlujo_ResuelveWorkflowConId()
    {
        var agentes = new[] { Agente("COMERCIAL-01", 1008, "SqlQueryTool") };
        agentes[0].AgentesWorkflows.Add(new AgenteWorkflow { IdAsistente = 1008, IdWorkflow = 7, Activo = true });
        var wfRepo = RepoWorkflow();
        wfRepo.Setup(r => r.GetActivosAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Workflow>
            {
                new() { IdWorkflow = 7, Codigo = "REP-CLI", Nombre = "Reporte clientes", Disparadores = "reporte de clientes;generar reporte", Estado = EstadoWorkflow.Activo }
            });
        var builder = new PlanBuilder(
            RepoAsistentes(agentes).Object,
            OllamaStub().Object,
            Logger<PlanBuilder>().Object,
            RepoConexion().Object,
            wfRepo.Object);

        var plan = await builder.ConstruirAsync("quiero automatizar el flujo con el reporte de clientes", 1, CancellationToken.None);

        var pasoWf = plan.Pasos.FirstOrDefault(p => p.Tipo == "Workflow");
        Assert.NotNull(pasoWf);
        Assert.Equal(7, pasoWf!.IdWorkflow);
    }

    [Fact]
    public async Task PlanBuilder_WorkflowSinAsignacion_OmitePasoWorkflow()
    {
        var agentes = new[] { Agente("COMERCIAL-01", 1008, "SqlQueryTool") };
        var wfRepo = RepoWorkflow();
        wfRepo.Setup(r => r.GetActivosAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Workflow>
            {
                new() { IdWorkflow = 7, Codigo = "REP-CLI", Nombre = "Reporte clientes", Disparadores = "reporte de clientes", Estado = EstadoWorkflow.Activo }
            });
        var builder = new PlanBuilder(
            RepoAsistentes(agentes).Object,
            OllamaStub().Object,
            Logger<PlanBuilder>().Object,
            RepoConexion().Object,
            wfRepo.Object);

        // Coincide la frase pero el agente NO tiene el workflow asignado.
        var plan = await builder.ConstruirAsync("quiero automatizar el reporte de clientes", 1, CancellationToken.None);

        Assert.DoesNotContain(plan.Pasos, p => p.Tipo == "Workflow");
    }

    [Fact]
    public async Task PlanBuilder_ObjetivoConFlujoSinCoincidencia_OmitePasoWorkflow()
    {
        var agentes = new[] { Agente("COMERCIAL-01", 1008, "SqlQueryTool") };
        var builder = new PlanBuilder(
            RepoAsistentes(agentes).Object,
            OllamaStub().Object,
            Logger<PlanBuilder>().Object,
            RepoConexion().Object,
            RepoWorkflow().Object);

        var plan = await builder.ConstruirAsync("quiero automatizar el flujo de nóminas", 1, CancellationToken.None);

        Assert.DoesNotContain(plan.Pasos, p => p.Tipo == "Workflow");
    }

    [Fact]
    public async Task PlanBuilder_ObjetivoParafraseado_RefinaIntencionesConLLM()
    {
        var agentes = new[] { Agente("SOPORTE-01", 2005, "DocumentSearchTool") };
        var ollama = new Mock<IOllamaService>();
        ollama.Setup(o => o.SendMessageAsync(It.IsAny<IEnumerable<Mensaje>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("{\"rag\":true,\"reporte\":false,\"riesgo\":false,\"workflow\":false,\"agregacion\":false,\"aprobacion\":false}");
        var builder = new PlanBuilder(
            RepoAsistentes(agentes).Object,
            ollama.Object,
            Logger<PlanBuilder>().Object,
            RepoConexion().Object,
            RepoWorkflow().Object);

        // "síntesis" no es keyword de reporte/RAG: solo el LLM rescata la intención.
        var plan = await builder.ConstruirAsync("necesito una síntesis del estado de cuenta", 1, CancellationToken.None);

        Assert.Contains(plan.Pasos, p => p.CodigoHerramienta == "DocumentSearchTool");
    }

    [Fact]
    public async Task Supervisor_FalloPaso_ReintentaHastaMaximoYLuegoFalla()
    {
        var planRepo = new Mock<IPlanRepository>();
        var stepRepo = new Mock<IPlanStepRepository>();
        var logRepo = new Mock<IPlanExecutionLogRepository>();
        var supervisor = new ExecutionSupervisor(
            planRepo.Object, stepRepo.Object, logRepo.Object,
            maxReintentos: 2, tiempoEntreIntentosMs: 1);

        var plan = new Plan { IdPlan = 1 };
        var paso = new PlanStep { IdStep = 9, IdPlan = 1, Orden = 1, Tipo = "Tool", Nombre = "Paso", Estado = "Error" };

        Assert.True(await supervisor.ManejarFalloPasoAsync(plan, paso, "boom", CancellationToken.None));
        Assert.Equal("Pendiente", paso.Estado);
        Assert.True(await supervisor.ManejarFalloPasoAsync(plan, paso, "boom", CancellationToken.None));
        Assert.False(await supervisor.ManejarFalloPasoAsync(plan, paso, "boom", CancellationToken.None));
        Assert.Equal("Error", paso.Estado);
    }

    [Fact]
    public async Task PlanValidator_PlanValido_SinErrores()
    {
        var agentes = new[] { Agente("COMERCIAL-01", 1008, "SqlQueryTool") };
        var validator = new PlanValidator(
            RepoAsistentes(agentes).Object,
            AuthStub(permitido: true).Object,
            RepoUsuario(UsuarioAdmin()).Object,
            RepoPolitica().Object,
            RepoWorkflow().Object);
        var plan = new Plan
        {
            IdUsuario = 1,
            Pasos = new List<PlanStep> { new() { Orden = 0, Tipo = "Agent", Nombre = "A", IdAsistente = 1008 } },
            Dependencias = new List<PlanDependency>()
        };

        var r = await validator.ValidarAsync(plan, CancellationToken.None);

        Assert.True(r.Valido);
        Assert.Empty(r.Errores);
    }

    [Fact]
    public async Task PlanValidator_AgenteInexistente_ReportaError()
    {
        var agentes = new[] { Agente("COMERCIAL-01", 1008, "SqlQueryTool") };
        var validator = new PlanValidator(
            RepoAsistentes(agentes).Object,
            AuthStub(permitido: true).Object,
            RepoUsuario(UsuarioAdmin()).Object,
            RepoPolitica().Object,
            RepoWorkflow().Object);
        var plan = new Plan
        {
            IdUsuario = 1,
            Pasos = new List<PlanStep> { new() { Orden = 0, Tipo = "Agent", Nombre = "A", IdAsistente = 9999 } },
            Dependencias = new List<PlanDependency>()
        };

        var r = await validator.ValidarAsync(plan, CancellationToken.None);

        Assert.False(r.Valido);
        Assert.Contains(r.Errores, e => e.Contains("9999"));
    }

    [Fact]
    public async Task PlanValidator_CicloDetectado_Invalido()
    {
        var agentes = new[] { Agente("COMERCIAL-01", 1008, "SqlQueryTool") };
        var validator = new PlanValidator(
            RepoAsistentes(agentes).Object,
            AuthStub(permitido: true).Object,
            RepoUsuario(UsuarioAdmin()).Object,
            RepoPolitica().Object,
            RepoWorkflow().Object);
        var plan = new Plan
        {
            IdUsuario = 1,
            Pasos = new List<PlanStep>
            {
                new() { Orden = 0, Tipo = "Agent", Nombre = "A", IdAsistente = 1008 },
                new() { Orden = 1, Tipo = "Agent", Nombre = "B", IdAsistente = 1008 }
            },
            Dependencias = new List<PlanDependency>
            {
                new() { StepOrigen = 0, StepDestino = 1 },
                new() { StepOrigen = 1, StepDestino = 0 }
            }
        };

        var r = await validator.ValidarAsync(plan, CancellationToken.None);

        Assert.False(r.Valido);
        Assert.Contains(r.Errores, e => e.Contains("ciclo"));
    }

    [Fact]
    public void ExecutionGraphBuilder_MapeaPasosYDependencias()
    {
        var builder = new ExecutionGraphBuilder();
        var plan = new Plan
        {
            Pasos = new List<PlanStep>
            {
                new() { Orden = 0, Tipo = "Agent", Nombre = "A", IdAsistente = 1008 },
                new() { Orden = 1, Tipo = "Tool", Nombre = "B", IdAsistente = 1008, CodigoHerramienta = "SqlQueryTool" }
            },
            Dependencias = new List<PlanDependency> { new() { StepOrigen = 0, StepDestino = 1 } }
        };

        var grafo = builder.Construir(plan);

        Assert.Equal(2, grafo.Nodos.Count);
        Assert.Contains(grafo.Nodos, n => n.IdNodo == 1 && n.DependeDe.Contains(0));
    }

    [Fact]
    public async Task ExecutionSupervisor_Reintentos_AgotanYMarcaError()
    {
        var planRepo = new Mock<IPlanRepository>();
        var stepRepo = new Mock<IPlanStepRepository>();
        var logRepo = new Mock<IPlanExecutionLogRepository>();
        var supervisor = new ExecutionSupervisor(planRepo.Object, stepRepo.Object, logRepo.Object, maxReintentos: 1, tiempoEntreIntentosMs: 1);

        var plan = new Plan { IdPlan = 1 };
        var paso = new PlanStep { IdStep = 10, Orden = 0, Nombre = "X", Estado = "EnEjecucion" };

        // Primer fallo -> reintenta (Intentos=1 <= max 1)
        var reintenta = await supervisor.ManejarFalloPasoAsync(plan, paso, "fallo1", CancellationToken.None);
        Assert.True(reintenta);
        Assert.Equal("Pendiente", paso.Estado);

        // Segundo fallo -> ya agotó reintentos
        var reintenta2 = await supervisor.ManejarFalloPasoAsync(plan, paso, "fallo2", CancellationToken.None);
        Assert.False(reintenta2);
        Assert.Equal("Error", paso.Estado);
    }

    [Fact]
    public void ExecutionGraph_RamasIndependientes_CompartenCapaParalela()
    {
        // SQL y RAG sin dependencias entre sí deben quedar en la misma capa (paralelo),
        // y la entrega en una capa posterior (convergencia).
        var builder = new ExecutionGraphBuilder();
        var plan = new Plan
        {
            Pasos = new List<PlanStep>
            {
                new() { Orden = 0, Tipo = "Coordination", Nombre = "Coordinar", IdAsistente = 1 },
                new() { Orden = 1, Tipo = "Tool", Nombre = "Consultar datos", IdAsistente = 1, CodigoHerramienta = "SqlQueryTool" },
                new() { Orden = 2, Tipo = "RAG", Nombre = "Consultar docs", IdAsistente = 1, CodigoHerramienta = "DocumentSearchTool" },
                new() { Orden = 3, Tipo = "Agent", Nombre = "Entregar", IdAsistente = 1 }
            },
            Dependencias = new List<PlanDependency>
            {
                new() { StepOrigen = 0, StepDestino = 1 },
                new() { StepOrigen = 0, StepDestino = 3 },
                new() { StepOrigen = 1, StepDestino = 3 }
            }
        };

        var capas = builder.Construir(plan).ObtenerCapas();

        var capaRag = capas.First(c => c.Any(n => n.IdNodo == 2));
        Assert.Contains(capaRag, n => n.IdNodo == 0); // RAG en paralelo con Coordinación (capa 0)
        var capaEntrega = capas.First(c => c.Any(n => n.IdNodo == 3));
        Assert.DoesNotContain(capaEntrega, n => n.IdNodo == 1 || n.IdNodo == 2); // convergencia posterior
    }

    [Fact]
    public async Task Validator_UsuarioSinPermisos_RechazaAntesDeEjecutar()
    {
        var usuario = new Usuario
        {
            IdUsuario = 2,
            UsuarioNombre = "sinpermisos",
            Nombres = "Sin",
            Apellidos = "Permisos",
            Activo = true,
            UsuarioRoles = new List<UsuarioRol>()
        };
        var validator = new PlanValidator(
            RepoAsistentes(Agente("AG", 1, "SqlQueryTool")).Object,
            AuthStub(permitido: false).Object,
            RepoUsuario(usuario).Object,
            RepoPolitica().Object,
            RepoWorkflow().Object);

        var plan = new Plan
        {
            IdPlan = 9,
            IdUsuario = 2,
            Pasos = new List<PlanStep>
            {
                new() { Orden = 0, Tipo = "Agent", Nombre = "Entregar", IdAsistente = 1 },
                new() { Orden = 1, Tipo = "Tool", Nombre = "Consultar", IdAsistente = 1, CodigoHerramienta = "SqlQueryTool" }
            },
            Dependencias = new List<PlanDependency>()
        };

        var r = await validator.ValidarAsync(plan, CancellationToken.None);

        Assert.False(r.Valido);
        Assert.NotEmpty(r.Errores);
    }

    [Fact]
    public async Task ReportTool_GeneraPdfValido()
    {
        // QuestPDF exige licencia configurada (en API se hace en Program.cs).
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

        byte[]? guardado = null;
        var storage = new Mock<IFileStorageService>();
        storage.Setup(s => s.SaveFileAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>()))
            .Callback<Stream, string, string>((stream, _, _) =>
            {
                using var ms = new MemoryStream();
                stream.CopyTo(ms);
                guardado = ms.ToArray();
            })
            .ReturnsAsync("reportes/reporte_test.pdf");

        var tool = new ReportTool(storage.Object);
        var resultado = await tool.ExecuteAsync(new ToolExecutionRequest
        {
            HerramientaCodigo = "ReportTool",
            Parametros = new Dictionary<string, object?>
            {
                ["titulo"] = "Reporte de prueba",
                ["datos"] = "Total empleados: 5"
            },
            IdUsuario = 1
        });

        Assert.True(resultado.Exitoso);
        Assert.NotNull(guardado);
        Assert.True(guardado.Length > 1000);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(guardado, 0, 4));
    }
}
