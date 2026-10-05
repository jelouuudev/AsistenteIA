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
/// selecciona a los colaboradores correctos por SIMILITUD con la descripción de
/// su herramienta (no por palabras clave del texto). Aísla la lógica de score +
/// reglas del runtime (Include de herramientas, DbContext, etc.).
/// </summary>
public class AgentSelectorColaboradoresTests
{
    // Descripciones reales de las herramientas (mismo texto que la BD): el
    // enrutamiento se mide contra ellas, no contra un vocabulario en código.
    private const string DescSql = "Ejecuta consultas SELECT de solo lectura sobre la base de datos empresarial autorizada.";
    private const string DescDoc = "Recupera fragmentos de documentos internos mediante búsqueda semántica (Motor RAG).";
    private const string DescRep = "Genera reportes estructurados en Markdown a partir de datos obtenidos.";

    /// <summary>
    /// Pregunta que mezcla reporting y consulta a un manual: similitud media con la
    /// descripción de ReportTool (0.72) y de DocumentSearchTool (0.62), baja con
    /// SqlQueryTool (0.31). Se.recruitan Reportes y Soporte; el agente de datos no.
    /// </summary>
    private static EmbeddingsPorEje EmbeddingsPreguntaReporte() =>
        new EmbeddingsPorEje()
            .Eje(DescSql, 0).Eje(DescDoc, 1).Eje(DescRep, 2)
            .Vector("Analiza las ventas del mes, prepara un resumen ejecutivo y compáralo con el procedimiento del manual",
                    new[] { 0.30f, 0.60f, 0.70f })
            .Vector("Muéstrame las ventas del mes y prepárame un resumen",
                    new[] { 0.20f, 0.10f, 0.95f });

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

        // Enrutamiento estructural: cat�logo de herramientas (descripciones de la BD)
        // + embeddings deterministas por eje de capacidad.
        var cat = EmbeddingsPorEje.Catalogo(
            ("SqlQueryTool", DescSql, "ConsultaSQL"),
            ("DocumentSearchTool", DescDoc, "ConsultaDocumental"),
            ("ReportTool", DescRep, "Reporte"));
        var emb = EmbeddingsPreguntaReporte();
        var selector = new AgentSelector(asistenteRepo.Object, reglasRepo.Object, auth.Object, emb, cat.Object);

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

        // Enrutamiento estructural: cat�logo de herramientas (descripciones de la BD)
        // + embeddings deterministas por eje de capacidad.
        var cat = EmbeddingsPorEje.Catalogo(
            ("SqlQueryTool", DescSql, "ConsultaSQL"),
            ("DocumentSearchTool", DescDoc, "ConsultaDocumental"),
            ("ReportTool", DescRep, "Reporte"));
        var emb = EmbeddingsPreguntaReporte();        var selector = new AgentSelector(asistenteRepo.Object, reglasRepo.Object, auth.Object, emb, cat.Object);

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
