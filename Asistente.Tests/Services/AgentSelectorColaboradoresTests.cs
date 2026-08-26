using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Asistente.Application.Interfaces;
using Asistente.Application.Orchestrator;
using Asistente.Domain.Entities;
using Asistente.Domain.Interfaces;
using Moq;
using Xunit;

namespace Asistente.Tests.Services;

/// <summary>
/// Diagnóstico aislado del AgentSelector: confirma que para la pregunta del RF
/// selecciona a los colaboradores correctos (Reportes por "resumen", Soporte por "manual").
/// Aísla la lógica de score + reglas del runtime (Include de herramientas, DbContext, etc.).
/// </summary>
public class AgentSelectorColaboradoresTests
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
                Herramienta = new Herramienta { Codigo = h }
            }).ToList()
        };

    [Fact]
    public async Task Selecciona_Reportes_Y_Soporte_Para_Pregunta_RF()
    {
        // Arrange
        var comercial = Agente("COMERCIAL-01", 1008, "SqlQueryTool", "ReportTool");
        var reportes = Agente("REPORTES-01", 2006, "ReportTool");
        var soporte = Agente("SOPORTE-01", 2005, "DocumentSearchTool");
        var rrhh = Agente("RRHH-01", 1009, "DocumentSearchTool");

        var asistenteRepo = new Mock<IAsistenteRepository>();
        asistenteRepo.Setup(r => r.GetByIdAsync(1008)).ReturnsAsync(comercial);
        asistenteRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Asistente.Domain.Entities.Asistente>
            { comercial, reportes, soporte, rrhh });
        // Carga determinista de herramientas por agente (nuevo método del selector).
        asistenteRepo.Setup(r => r.GetHerramientasActivasAsync(1008)).ReturnsAsync(new List<string> { "SqlQueryTool", "ReportTool" });
        asistenteRepo.Setup(r => r.GetHerramientasActivasAsync(2006)).ReturnsAsync(new List<string> { "ReportTool" });
        asistenteRepo.Setup(r => r.GetHerramientasActivasAsync(2005)).ReturnsAsync(new List<string> { "DocumentSearchTool" });
        asistenteRepo.Setup(r => r.GetHerramientasActivasAsync(1009)).ReturnsAsync(new List<string> { "DocumentSearchTool" });

        var reglasRepo = new Mock<IAgentCollaborationRuleRepository>();
        // Comercial -> Reportes: permitido
        reglasRepo.Setup(r => r.EstaPermitidoAsync(1008, 2006, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        // Comercial -> Soporte: permitido
        reglasRepo.Setup(r => r.EstaPermitidoAsync(1008, 2005, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        // Comercial -> RRHH: denegado
        reglasRepo.Setup(r => r.EstaPermitidoAsync(1008, 1009, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var auth = new Mock<IAutorizacionService>();

        var selector = new AgentSelector(asistenteRepo.Object, reglasRepo.Object, auth.Object);

        var request = new AgentRequest
        {
            IdAgentePrincipal = 1008,
            IdUsuario = 1,
            Pregunta = "Analiza las ventas del mes, prepara un resumen ejecutivo y compáralo con el procedimiento del manual"
        };

        // Act
        var candidatos = (await selector.SelectAgentsAsync(request)).ToList();

        // Assert
        var nombres = candidatos.Select(c => c.Nombre).ToList();
        Assert.Contains("REPORTES-01", nombres);
        Assert.Contains("SOPORTE-01", nombres);
        Assert.DoesNotContain("RRHH-01", nombres);
    }

    [Fact]
    public async Task Selecciona_Reportes_Para_Pregunta_Corta_Con_Resumen()
    {
        var comercial = Agente("COMERCIAL-01", 1008, "SqlQueryTool", "ReportTool");
        var reportes = Agente("REPORTES-01", 2006, "ReportTool");

        var asistenteRepo = new Mock<IAsistenteRepository>();
        asistenteRepo.Setup(r => r.GetByIdAsync(1008)).ReturnsAsync(comercial);
        asistenteRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Asistente.Domain.Entities.Asistente>
            { comercial, reportes });
        asistenteRepo.Setup(r => r.GetHerramientasActivasAsync(1008)).ReturnsAsync(new List<string> { "SqlQueryTool", "ReportTool" });
        asistenteRepo.Setup(r => r.GetHerramientasActivasAsync(2006)).ReturnsAsync(new List<string> { "ReportTool" });

        var reglasRepo = new Mock<IAgentCollaborationRuleRepository>();
        reglasRepo.Setup(r => r.EstaPermitidoAsync(1008, 2006, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var auth = new Mock<IAutorizacionService>();
        var selector = new AgentSelector(asistenteRepo.Object, reglasRepo.Object, auth.Object);

        var request = new AgentRequest
        {
            IdAgentePrincipal = 1008,
            IdUsuario = 1,
            Pregunta = "Muéstrame las ventas del mes y prepárame un resumen"
        };

        var candidatos = (await selector.SelectAgentsAsync(request)).ToList();

        Assert.Contains("REPORTES-01", candidatos.Select(c => c.Nombre));
    }
}
