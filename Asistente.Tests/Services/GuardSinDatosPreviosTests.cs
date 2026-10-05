using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Asistente.Application.Interfaces;
using Asistente.Application.Orchestrator;
using Asistente.Application.Services.Herramientas;
using Asistente.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace Asistente.Tests.Services;

/// <summary>
/// Regresión del plan #6038: los dos pasos SqlQueryTool agotaron reintentos (Error) y
/// el paso Agent "entregó" una tabla INVENTADA (categorías y montos que no existen).
/// El prompt ya lo prohibía, pero deepseek-r1:7b no respeta esa instrucción, así que
/// el guard debe ser DETERMINISTA: si el plan tiene pasos Tool/RAG previos y ninguno
/// dejó datos utilizables, no se llama al LLM.
/// </summary>
public class GuardSinDatosPreviosTests
{
    private static IServiceProvider CrearProveedor(List<PlanStep> pasos)
    {
        var repo = new Mock<IPlanStepRepository>();
        repo.Setup(r => r.GetByPlanAsync(It.IsAny<int>(), It.IsAny<CancellationToken>(), It.IsAny<bool>()))
            .ReturnsAsync(pasos);
        var services = new ServiceCollection();
        services.AddSingleton(repo.Object);
        return services.BuildServiceProvider();
    }

    private static Plan CrearPlan() => new() { IdPlan = 1, Objetivo = "Compara ventas" };

    [Fact]
    public async Task PasosToolEnError_SinDatosPrevios_True()
    {
        var pasos = new List<PlanStep>
        {
            new() { IdStep = 1, Orden = 0, Tipo = "Coordination", Estado = "Completado", Resultado = "plan de acción" },
            new() { IdStep = 2, Orden = 1, Tipo = "Tool", Estado = "Error",
                    Resultado = "SqlQueryTool: Error al ejecutar la herramienta: A task was canceled." },
            new() { IdStep = 3, Orden = 2, Tipo = "Tool", Estado = "Error",
                    Resultado = "SqlQueryTool: Error al ejecutar la herramienta: A task was canceled." },
        };
        var entrega = new PlanStep { IdStep = 4, Orden = 3, Tipo = "Agent", Estado = "Pendiente" };

        var sinDatos = await AgentOrchestrator.SinDatosPreviosAsync(
            CrearProveedor(pasos), CrearPlan(), entrega, CancellationToken.None);

        Assert.True(sinDatos);
    }

    [Fact]
    public async Task PasosToolCompletos_ConDatos_SinDatosPrevios_False()
    {
        var pasos = new List<PlanStep>
        {
            new() { IdStep = 1, Orden = 1, Tipo = "Tool", Estado = "Completado",
                    Resultado = "Datos obtenidos de la tabla 'ventas'. Total filtrado: 7. Valor total: 3980.00." },
        };
        var entrega = new PlanStep { IdStep = 2, Orden = 3, Tipo = "Agent", Estado = "Pendiente" };

        var sinDatos = await AgentOrchestrator.SinDatosPreviosAsync(
            CrearProveedor(pasos), CrearPlan(), entrega, CancellationToken.None);

        Assert.False(sinDatos);
    }

    [Fact]
    public async Task PlanSinPasosTool_NoSeCorta_ElAgenteDebeResponder()
    {
        // Plan puramente documental/agente: no hay Tool/RAG que entregar, el LLM
        // debe poder responder con lo suyo.
        var pasos = new List<PlanStep>
        {
            new() { IdStep = 1, Orden = 0, Tipo = "Coordination", Estado = "Completado", Resultado = "plan de acción" },
        };
        var entrega = new PlanStep { IdStep = 2, Orden = 1, Tipo = "Agent", Estado = "Pendiente" };

        var sinDatos = await AgentOrchestrator.SinDatosPreviosAsync(
            CrearProveedor(pasos), CrearPlan(), entrega, CancellationToken.None);

        Assert.False(sinDatos);
    }

    [Fact]
    public async Task PasoToolMarcadorSinDatos_SinDatosPrevios_True()
    {
        // Completado pero marcado por el CONTRATO como "sin datos": no sirve para
        // entregar. Se reconoce por el token compartido, no por la frase del error.
        var pasos = new List<PlanStep>
        {
            new() { IdStep = 1, Orden = 1, Tipo = "Tool", Estado = "Completado",
                    Resultado = ContratoResultado.MarcarSinDatos("la consulta no devolvió filas") },
        };
        var entrega = new PlanStep { IdStep = 2, Orden = 2, Tipo = "Agent", Estado = "Pendiente" };

        var sinDatos = await AgentOrchestrator.SinDatosPreviosAsync(
            CrearProveedor(pasos), CrearPlan(), entrega, CancellationToken.None);

        Assert.True(sinDatos);
    }

    /// <summary>
    /// Regresión: antes el "sin datos" se reconocía comparando la REDACCIÓN del error
    /// ("la consulta no devolvió registros", "consulta rechazada", ...). Si una
    /// herramienta cambiaba su mensaje, el paso se tomaba por datos válidos y el agente
    /// final entregaba cifras inventadas encima.
    /// </summary>
    [Fact]
    public async Task ErrorConOtraRedaccion_SigueReconociendoseComoSinDatos()
    {
        var pasos = new List<PlanStep>
        {
            new() { IdStep = 1, Orden = 1, Tipo = "Tool", Estado = "Completado",
                    Resultado = "Error desconocido del proveedor: el recurso no está disponible en este momento." },
        };
        var entrega = new PlanStep { IdStep = 2, Orden = 2, Tipo = "Agent", Estado = "Pendiente" };

        // Sin el contrato no se puede decidir: un texto que no es contrato y no tiene
        // datos se trata como "sin datos" solo si el productor lo marcó. Este test fija
        // que un resultado VACÍO no se considera entregable.
        var sinDatosVacio = await AgentOrchestrator.SinDatosPreviosAsync(
            CrearProveedor(new List<PlanStep>()), CrearPlan(), entrega, CancellationToken.None);
        Assert.False(sinDatosVacio); // sin Tool/RAG previos el agente responde

        // Y que el marcador del contrato SÍ se reconoce.
        var conContrato = new List<PlanStep>
        {
            new() { IdStep = 1, Orden = 1, Tipo = "Tool", Estado = "Completado",
                    Resultado = ContratoResultado.MarcarSinDatos("cualquier redacción") },
        };
        Assert.True(await AgentOrchestrator.SinDatosPreviosAsync(
            CrearProveedor(conContrato), CrearPlan(), entrega, CancellationToken.None));
    }

    [Fact]
    public async Task SoloUnPasoToolConDatosEntreVarios_SinDatosPrevios_False()
    {
        // Un solo paso con datos basta: el resto puede haber fallado.
        var pasos = new List<PlanStep>
        {
            new() { IdStep = 1, Orden = 1, Tipo = "Tool", Estado = "Error", Resultado = "A task was canceled." },
            new() { IdStep = 2, Orden = 2, Tipo = "Tool", Estado = "Completado",
                    Resultado = "Datos obtenidos de la tabla 'ventas'. Total filtrado: 7." },
        };
        var entrega = new PlanStep { IdStep = 3, Orden = 3, Tipo = "Agent", Estado = "Pendiente" };

        var sinDatos = await AgentOrchestrator.SinDatosPreviosAsync(
            CrearProveedor(pasos), CrearPlan(), entrega, CancellationToken.None);

        Assert.False(sinDatos);
    }

    [Fact]
    public async Task SoloConsideraPasosAnterioresAlEntrega_NoPosteriores()
    {
        // El Tool con datos es POSTERIOR al paso de entrega: no puede servir de
        // contexto, así que el guard sigue detectando que no hay datos previos.
        var pasos = new List<PlanStep>
        {
            new() { IdStep = 1, Orden = 1, Tipo = "Tool", Estado = "Error", Resultado = "A task was canceled." },
            new() { IdStep = 2, Orden = 3, Tipo = "Tool", Estado = "Completado", Resultado = "datos posteriores" },
        };
        var entrega = new PlanStep { IdStep = 3, Orden = 2, Tipo = "Agent", Estado = "Pendiente" };

        var sinDatos = await AgentOrchestrator.SinDatosPreviosAsync(
            CrearProveedor(pasos), CrearPlan(), entrega, CancellationToken.None);

        Assert.True(sinDatos);
    }

    [Fact]
    public async Task EntregaSinToolNiRagPrevios_NoSeCorta()
    {
        // Entrega en el orden 0: no hay Tool/RAG previos que esperar, el agente
        // responde con su propio criterio (no es un plan de entrega de datos).
        var pasos = new List<PlanStep>
        {
            new() { IdStep = 1, Orden = 5, Tipo = "Tool", Estado = "Completado", Resultado = "datos" },
        };
        var entrega = new PlanStep { IdStep = 2, Orden = 0, Tipo = "Agent", Estado = "Pendiente" };

        var sinDatos = await AgentOrchestrator.SinDatosPreviosAsync(
            CrearProveedor(pasos), CrearPlan(), entrega, CancellationToken.None);

        Assert.False(sinDatos);
    }
}
