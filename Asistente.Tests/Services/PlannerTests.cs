using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Asistente.Application.Interfaces;
using Asistente.Application.Orchestrator.Planner;
using Asistente.Domain.Entities;
using Asistente.Domain.Interfaces;
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
        repo.Setup(r => r.GetHerramientasActivasAsync(It.IsAny<int>()))
            .ReturnsAsync((int id) => agentes.First(a => a.IdAsistente == id)
                .AsistentesHerramientas.Where(h => h.Activa).Select(h => h.Herramienta.Codigo).ToList());
        return repo;
    }

    private static Mock<IOllamaService> OllamaStub()
    {
        var m = new Mock<IOllamaService>();
        m.Setup(o => o.SendMessageAsync(It.IsAny<IEnumerable<Mensaje>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Razonamiento de prueba.");
        return m;
    }

    private static Mock<ILogger<T>> Logger<T>() where T : class => new();

    [Fact]
    public async Task PlanBuilder_DeteccionPorIntencion_CreaPasosEsperados()
    {
        var agentes = new[]
        {
            Agente("COMERCIAL-01", 1008, "SqlQueryTool", "ReportTool"),
            Agente("SOPORTE-….01", 2005, "DocumentSearchTool"),
            Agente("REPORTES-01", 2006, "ReportTool")
        };
        var builder = new PlanBuilder(RepoAsistentes(agentes).Object, OllamaStub().Object, Logger<PlanBuilder>().Object);

        var plan = await builder.ConstruirAsync(
            "Analiza las ventas del trimestre, compara el cumplimiento con el procedimiento documentado, genera un informe ejecutivo en PDF y resalta los riesgos detectados.",
            1, CancellationToken.None);

        Assert.NotEmpty(plan.Pasos);
        Assert.Contains(plan.Pasos, p => p.Tipo == "Tool" && p.CodigoHerramienta == "SqlQueryTool");
        Assert.Contains(plan.Pasos, p => p.Tipo == "RAG");
        Assert.Contains(plan.Pasos, p => p.Tipo == "Tool" && p.CodigoHerramienta == "ReportTool");
        Assert.True(plan.Pasos.Any(p => p.Orden == 0 && p.Tipo == "Agent"));
    }

    [Fact]
    public async Task PlanBuilder_AccionSensible_RequiereAprobacion()
    {
        var agentes = new[] { Agente("COMERCIAL-01", 1008, "SqlQueryTool") };
        var builder = new PlanBuilder(RepoAsistentes(agentes).Object, OllamaStub().Object, Logger<PlanBuilder>().Object);

        var plan = await builder.ConstruirAsync("Eliminar los registros de ventas obsoletos", 1, CancellationToken.None);

        Assert.True(plan.RequiereAprobacion);
        Assert.Contains(plan.Pasos, p => p.Tipo == "Approval");
    }

    [Fact]
    public async Task PlanValidator_PlanValido_SinErrores()
    {
        var agentes = new[] { Agente("COMERCIAL-01", 1008, "SqlQueryTool") };
        var validator = new PlanValidator(RepoAsistentes(agentes).Object);
        var plan = new Plan
        {
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
        var validator = new PlanValidator(RepoAsistentes(agentes).Object);
        var plan = new Plan
        {
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
        var validator = new PlanValidator(RepoAsistentes(agentes).Object);
        var plan = new Plan
        {
            Pasos = new List<PlanStep>
            {
                new() { Orden = 0, Tipo = "Agent", Nombre = "A", IdAsistente = 1008 },
                new() { Orden = 1, Tipo = "Agent", Nombre = "B", IdAsistente = 1008 }
            },
            // ciclo: 0->1 y 1->0
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
}
