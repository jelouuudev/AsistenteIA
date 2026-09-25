using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Asistente.Application.Interfaces;
using Asistente.Application.Orchestrator;
using Asistente.Domain.Entities;
using Asistente.Domain.Interfaces;
using Asistente.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Asistente.Tests.Services;

public class AgentSelectorOrchestratorIntegrationTests
{
    private static Asistente.Domain.Entities.Asistente Agente(int id, string codigo, params string[] herramientas) => new()
    {
        IdAsistente = id,
        Nombre = codigo,
        Codigo = codigo,
        Activo = true,
        Objetivo = codigo,
        AsistentesHerramientas = herramientas.Select(h => new AsistenteHerramienta
        {
            Activa = true,
            Herramienta = new Herramienta { Codigo = h }
        }).ToList()
    };

    private static Mock<IAsistenteRepository> RepoAsistentes(Asistente.Domain.Entities.Asistente principal, params Asistente.Domain.Entities.Asistente[] colaboradores)
    {
        var todos = new[] { principal }.Concat(colaboradores).ToList();
        var repo = new Mock<IAsistenteRepository>();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<int>())).ReturnsAsync((int id) => todos.First(a => a.IdAsistente == id));
        repo.Setup(r => r.GetAllAsync()).ReturnsAsync(todos);
        repo.Setup(r => r.GetHerramientasActivasAsync(It.IsAny<int>()))
            .ReturnsAsync((int id) => todos.First(a => a.IdAsistente == id)
                .AsistentesHerramientas.Where(h => h.Activa)
                .Select(h => h.Herramienta!.Codigo).ToList());
        return repo;
    }

    // ===== AgentSelector: selecciona por tipo de solicitud + reglas + permisos =====
    [Fact]
    public async Task AgentSelector_SeleccionaColaboradoresSegunIntencionYSoloAutorizados()
    {
        var comercial = Agente(2, "Comercial", "SqlQueryTool");
        var soporte = Agente(3, "Soporte", "DocumentSearchTool");
        var reportes = Agente(4, "Reportes", "ReportTool");
        var principal = Agente(1, "Principal", "ReportTool");
        var repo = RepoAsistentes(principal, comercial, soporte, reportes);

        var reglas = new Mock<IAgentCollaborationRuleRepository>();
        reglas.Setup(r => r.EstaPermitidoAsync(1, It.IsAny<int>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync((int o, int d, CancellationToken _) => d != 4); // bloquear Reportes (demo Regla 3)

        var auth = new Mock<IAutorizacionService>();
        auth.Setup(a => a.VerificarAsistenteAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResultadoAutorizacion { Permitido = true });

        var selector = new AgentSelector(repo.Object, reglas.Object, auth.Object);
        var candidatos = (await selector.SelectAgentsAsync(new AgentRequest
        {
            IdUsuario = 1,
            IdAgentePrincipal = 1,
            Pregunta = "Analiza las ventas del mes y compara con el procedimiento del manual"
        })).ToList();

        // Comercial (SQL) y Soporte (RAG) permitidos; Reportes bloqueado por regla.
        Assert.Contains(candidatos, c => c.IdAgente == 2);
        Assert.Contains(candidatos, c => c.IdAgente == 3);
        Assert.DoesNotContain(candidatos, c => c.IdAgente == 4);
    }

    // ===== AgentOrchestrator: orquesta y consolida (Regla 1: nunca agente->agente directo) =====
    [Fact]
    public async Task AgentOrchestrator_EjecutaGrafoYConsolidaUnaRespuesta()
    {
        var comercial = Agente(2, "Comercial", "SqlQueryTool");
        var soporte = Agente(3, "Soporte", "DocumentSearchTool");
        var principal = Agente(1, "Principal", "ReportTool");
        var repo = RepoAsistentes(principal, comercial, soporte);

        var reglas = new Mock<IAgentCollaborationRuleRepository>();
        reglas.Setup(r => r.EstaPermitidoAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        reglas.Setup(r => r.GetActivasAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<AgentCollaborationRule>());

        var auth = new Mock<IAutorizacionService>();
        auth.Setup(a => a.VerificarAsistenteAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResultadoAutorizacion { Permitido = true });

        var config = new ConfiguracionOrchestrator { MaxAgentesPorSolicitud = 5, MaxProfundidad = 3, EstrategiaError = "Continuar" };
        var configRepo = new Mock<IConfiguracionOrchestratorRepository>();
        configRepo.Setup(r => r.GetAsync()).ReturnsAsync(config);

        // ChatService mock: simula cada agente respondiendo (el Orchestrator es quien los invoca)
        var chat = new Mock<IChatService>();
        chat.Setup(c => c.ProcesarMensajeAsync(It.IsAny<MensajeRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MensajeRequest req, CancellationToken _) => new MensajeResponse
            {
                Exitoso = true,
                Respuesta = $"Respuesta del agente {req.IdAsistente}"
            });

        var execRepo = new Mock<IAgentExecutionRepository>();
        AgentExecution? executionCapturada = null;
        execRepo.Setup(r => r.AddAsync(It.IsAny<AgentExecution>(), It.IsAny<CancellationToken>()))
                .Callback<AgentExecution, CancellationToken>((e, _) => { e.IdExecution = 1; executionCapturada = e; })
                .ReturnsAsync((AgentExecution e, CancellationToken _) => e);
        execRepo.Setup(r => r.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((int id, CancellationToken _) => executionCapturada ?? new AgentExecution { IdExecution = id, Pasos = new List<AgentExecutionStep>(), Trazas = new List<AgentExecutionTrace>(), AgentePrincipal = principal });
        execRepo.Setup(r => r.UpdateAsync(It.IsAny<AgentExecution>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var stepRepo = new Mock<IAgentExecutionStepRepository>();
        stepRepo.Setup(r => r.AddAsync(It.IsAny<AgentExecutionStep>(), It.IsAny<CancellationToken>()))
                .Callback<AgentExecutionStep, CancellationToken>((s, _) =>
                {
                    if (executionCapturada != null)
                    {
                        s.IdExecution = executionCapturada.IdExecution;
                        executionCapturada.Pasos.Add(s);
                    }
                })
                .ReturnsAsync((AgentExecutionStep s, CancellationToken _) => s);
        stepRepo.Setup(r => r.UpdateAsync(It.IsAny<AgentExecutionStep>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var traceRepo = new Mock<IAgentExecutionTraceRepository>();
        traceRepo.Setup(r => r.AddAsync(It.IsAny<AgentExecutionTrace>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync((AgentExecutionTrace t, CancellationToken _) => t);
        traceRepo.Setup(r => r.GetByExecutionAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<AgentExecutionTrace>());

        var selector = new AgentSelector(repo.Object, reglas.Object, auth.Object);
        var aggregator = new ResponseAggregator();
        var contextManager = new ContextManager(reglas.Object);

        var orchestrator = new AgentOrchestrator(
            selector, aggregator, contextManager,
            execRepo.Object, stepRepo.Object, traceRepo.Object, reglas.Object,
            configRepo.Object, repo.Object, chat.Object,
            OllamaDisponibleMock(), Mock.Of<IServiceScopeFactory>(), new Mock<ILogger<AgentOrchestrator>>().Object);

        var result = await orchestrator.ExecuteAsync(new AgentRequest
        {
            IdUsuario = 1,
            IdAgentePrincipal = 1,
            Pregunta = "Analiza las ventas del mes y compáralo con el procedimiento del manual"
        });

        Assert.True(result.Exitoso);
        Assert.NotNull(result.RespuestaFinal);
        Assert.Contains("Respuesta del agente 1", result.RespuestaFinal); // principal invocado por el Orchestrator
        Assert.Contains("Respuesta del agente 2", result.RespuestaFinal); // Comercial
        Assert.Contains("Respuesta del agente 3", result.RespuestaFinal); // Soporte
        chat.Verify(c => c.ProcesarMensajeAsync(It.IsAny<MensajeRequest>(), It.IsAny<CancellationToken>()), Times.AtLeast(2));
        stepRepo.Verify(r => r.AddAsync(It.IsAny<AgentExecutionStep>(), It.IsAny<CancellationToken>()), Times.AtLeast(2));
    }

    private static IOllamaService OllamaDisponibleMock()
    {
        var mock = new Mock<IOllamaService>();
        mock.Setup(o => o.IsDisponibleAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        return mock.Object;
    }
}
