using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Asistente.Application.Interfaces;
using Asistente.Application.Orchestrator;
using Asistente.Domain.Entities;
using Moq;
using Xunit;

namespace Asistente.Tests.Services;

public class AgentOrchestratorTests
{
    // ===== ExecutionGraph: topología paralelo/secuencial y anti-ciclos =====
    [Fact]
    public void ExecutionGraph_CapaParalela_AgrupaIndependientes()
    {
        var g = new ExecutionGraph
        {
            Nodos = new List<ExecutionNode>
            {
                new() { IdNodo = 0, IdAgente = 1 },
                new() { IdNodo = 1, IdAgente = 2, DependeDe = new() { 0 } },
                new() { IdNodo = 2, IdAgente = 3, DependeDe = new() { 0 } },
                new() { IdNodo = 3, IdAgente = 4, DependeDe = new() { 1, 2 } }
            }
        };
        var capas = g.ObtenerCapas();
        Assert.Equal(3, capas.Count);              // capa0: nodo0; capa1: nodo1,nodo2 (paralelos); capa2: nodo3
        Assert.Single(capas[0]);
        Assert.Equal(2, capas[1].Count);           // 1 y 2 en paralelo
        Assert.Single(capas[2]);
        Assert.Equal(3, g.ProfundidadMaxima());
    }

    [Fact]
    public void ExecutionGraph_Ciclo_LanzaExcepcion()
    {
        var g = new ExecutionGraph
        {
            Nodos = new List<ExecutionNode>
            {
                new() { IdNodo = 0, IdAgente = 1, DependeDe = new() { 1 } },
                new() { IdNodo = 1, IdAgente = 2, DependeDe = new() { 0 } }
            }
        };
        Assert.Throws<InvalidOperationException>(() => g.ObtenerCapas()); // Regla: prevención de ciclos
    }

    // ===== ResponseAggregator: consolidación y eliminación de duplicados =====
    [Fact]
    public async Task ResponseAggregator_EliminaDuplicadosYOrdena()
    {
        var agg = new ResponseAggregator();
        var exec = new AgentExecution
        {
            IdExecution = 1,
            Pasos = new List<AgentExecutionStep>
            {
                new() { Orden = 1, Agente = new Asistente.Domain.Entities.Asistente { Nombre = "Comercial" }, Accion = "SQL", Resultado = "Ventas: 100", Estado = "Completado" },
                new() { Orden = 2, Agente = new Asistente.Domain.Entities.Asistente { Nombre = "Reportes" }, Accion = "Resumen", Resultado = "Ventas: 100", Estado = "Completado" }, // duplicado
                new() { Orden = 3, Agente = new Asistente.Domain.Entities.Asistente { Nombre = "Soporte" }, Accion = "RAG", Resultado = "Procedimiento X", Estado = "Completado" }
            }
        };
        var r = await agg.BuildFinalResponseAsync(exec);
        Assert.Contains("Comercial", r);
        Assert.Contains("Soporte", r);
        Assert.Contains("Procedimiento X", r);
        // El duplicado de "Ventas: 100" no debe aparecer dos veces como sección
        Assert.Single(r.Split("Comercial").Skip(1));
    }

    // ===== ContextManager: comparte solo si hay autorización (Regla 4) =====
    [Fact]
    public async Task ContextManager_NoComparteSinReglaExplicita()
    {
        var reglas = new Mock<IAgentCollaborationRuleRepository>();
        reglas.Setup(r => r.EstaPermitidoAsync(2, 3, It.IsAny<CancellationToken>())).ReturnsAsync(false); // deny by default
        var cm = new ContextManager(reglas.Object);

        var ctx = new SharedContext
        {
            IdUsuario = 1,
            PreguntaOriginal = "p",
            ResultadosPrevios = new List<ContextoParcial> { new() { IdAgente = 2, NombreAgente = "Comercial", Contenido = "dato" } }
        };
        var ctxAgente = await cm.BuildContextForAgentAsync(3, ctx);
        Assert.Empty(ctxAgente.ContextoAutorizado); // no autorizado => no se comparte
    }

    [Fact]
    public async Task ContextManager_ComparteConReglaPermitida()
    {
        var reglas = new Mock<IAgentCollaborationRuleRepository>();
        reglas.Setup(r => r.EstaPermitidoAsync(2, 3, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var cm = new ContextManager(reglas.Object);

        var ctx = new SharedContext
        {
            IdUsuario = 1,
            PreguntaOriginal = "p",
            ResultadosPrevios = new List<ContextoParcial> { new() { IdAgente = 2, NombreAgente = "Comercial", Contenido = "dato SQL" } }
        };
        var ctxAgente = await cm.BuildContextForAgentAsync(3, ctx);
        Assert.Single(ctxAgente.ContextoAutorizado);
    }
}
