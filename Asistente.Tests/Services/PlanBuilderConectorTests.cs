using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Asistente.Application.Interfaces;
using Asistente.Application.Orchestrator;
using Asistente.Application.Orchestrator.Planner;
using Asistente.Domain.Entities;
using Asistente.Domain.Interfaces;
using Asistente.Tests.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace Asistente.Tests.Services;

/// <summary>
/// El Planner emite pasos GatewayConnectorTool sin vocabulario fijo (ETAPA 20):
/// por mención determinista del CÓDIGO registrado o por similitud semántica
/// contra la firma viva del conector. El recurso/método nunca se inventan:
/// viajan solo el código y la operación; los defectos los pone el conector.
/// </summary>
public class PlanBuilderConectorTests
{
    private static Asistente.Domain.Entities.Asistente Agente() => new()
    {
        IdAsistente = 1, Codigo = "PRINCIPAL", Nombre = "Principal", Activo = true
    };

    private static Mock<IAsistenteRepository> RepoAsistentes()
    {
        var repo = new Mock<IAsistenteRepository>();
        var agente = Agente();
        repo.Setup(r => r.GetAllAsync()).ReturnsAsync(new[] { agente });
        repo.Setup(r => r.GetByIdAsync(It.IsAny<int>())).ReturnsAsync(agente);
        return repo;
    }

    private static Mock<IConexionBaseDatosRepository> RepoConexionesVacias()
    {
        var repo = new Mock<IConexionBaseDatosRepository>();
        repo.Setup(r => r.GetActivasAsync()).ReturnsAsync(Array.Empty<ConexionBaseDatos>());
        return repo;
    }

    private static Mock<IWorkflowRepository> RepoWorkflowsVacios()
    {
        var repo = new Mock<IWorkflowRepository>();
        repo.Setup(r => r.GetActivosAsync()).ReturnsAsync(new List<Workflow>());
        return repo;
    }

    private static Mock<IDocumentoRepository> RepoDocumentosVacios()
    {
        var repo = new Mock<IDocumentoRepository>();
        repo.Setup(r => r.GetAllAsync()).ReturnsAsync(Array.Empty<Documento>());
        repo.Setup(r => r.GetContenidosIndexadosAsync(It.IsAny<int>()))
            .ReturnsAsync(Array.Empty<DocumentoContenidoResumen>());
        return repo;
    }

    private static Mock<IOllamaService> OllamaCon(bool conector)
    {
        var mock = new Mock<IOllamaService>();
        mock.Setup(o => o.SendMessageAsync(It.IsAny<IEnumerable<Mensaje>>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<double?>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("{\"tablas\":[],\"rag\":false,\"reporte\":false,\"riesgo\":false,\"workflow\":false,\"agregacion\":false,\"aprobacion\":false,\"conector\":" + (conector ? "true" : "false") + ",\"subconsultas\":[]}");
        mock.Setup(o => o.IsDisponibleAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        return mock;
    }

    private static Mock<IConnectorRepository> RepoConectores(params Connector[] conectores)
    {
        var repo = new Mock<IConnectorRepository>();
        repo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(conectores.ToList());
        repo.Setup(r => r.GetByCodigoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string c, CancellationToken _) =>
                conectores.FirstOrDefault(x => x.Codigo.Equals(c, StringComparison.OrdinalIgnoreCase)));
        return repo;
    }

    private static Connector ConectorDemo(string codigo = "API_DEMO", string nombre = "API demo",
        string descripcion = "Servicio interno de estado", string tipo = "REST", bool activo = true)
    {
        var c = new Connector
        {
            IdConnector = 11, Codigo = codigo, Nombre = nombre,
            Descripcion = descripcion, Tipo = tipo, Activo = activo
        };
        c.Configuraciones.Add(new Domain.Entities.ConnectorConfiguration
            { Clave = "BaseUrl", Valor = "https://demo.local" });
        c.Configuraciones.Add(new Domain.Entities.ConnectorConfiguration
            { Clave = "RecursoDefecto", Valor = "api/health" });
        return c;
    }

    private static PlanBuilder Crear(Mock<IConnectorRepository> repoConectores,
        Mock<IOllamaService> ollama, IEmbeddingProvider? embeddings = null) => new(
        RepoAsistentes().Object,
        ollama.Object,
        new Mock<ILogger<PlanBuilder>>().Object,
        RepoConexionesVacias().Object,
        RepoWorkflowsVacios().Object,
        embeddings,
        null, null,
        RepoDocumentosVacios().Object,
        repoConectores.Object);

    [Fact]
    public async Task MencionCodigo_EmitePasoGatewayConEntradaYDependencia()
    {
        var builder = Crear(RepoConectores(ConectorDemo()), OllamaCon(false));

        var plan = await builder.ConstruirAsync(
            "consulta el estado del servicio usando el conector API_DEMO", 1, CancellationToken.None);

        var paso = Assert.Single(plan.Pasos.Where(p =>
            string.Equals(p.CodigoHerramienta, "GatewayConnectorTool", StringComparison.OrdinalIgnoreCase)));
        Assert.Equal("Tool", paso.Tipo);
        using var doc = JsonDocument.Parse(paso.Entrada!);
        Assert.Equal("API_DEMO", doc.RootElement.GetProperty("conector").GetString());
        var entrega = plan.Pasos.First(p => p.Nombre.Contains("Entregar"));
        Assert.Contains(plan.Dependencias, d =>
            d.StepOrigen == paso.Orden && d.StepDestino == entrega.Orden);
    }

    [Fact]
    public async Task SinMencionYSinSimilitud_NoEmitePasoAunqueElFlagVengaEnTrue()
    {
        // El flag del LLM solo no inventa conectores: sin mención ni similitud
        // contra el catálogo no hay paso. (Embeddings nulos = sin vía semántica.)
        var builder = Crear(RepoConectores(ConectorDemo()), OllamaCon(true), null);

        var plan = await builder.ConstruirAsync("cuentame algo del sistema", 1, CancellationToken.None);

        Assert.DoesNotContain(plan.Pasos, p =>
            string.Equals(p.CodigoHerramienta, "GatewayConnectorTool", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task SimilitudFuerte_EmitePasoSinMencionNiFlag()
    {
        // Similitud 1.0 entre objetivo y firma viva del conector (>= 0.75):
        // el paso se emite aunque el LLM no haya marcado la intención.
        const string objetivo = "revisa el estado del servicio interno";
        var firma = "API_DEMO API demo Servicio interno de estado REST";
        var vector = new float[] { 0f, 1f, 0f };
        var embeddings = new EmbeddingsPorEje()
            .Vector(objetivo, vector)
            .Vector(firma, vector);
        var builder = Crear(RepoConectores(ConectorDemo()), OllamaCon(false), embeddings);

        var plan = await builder.ConstruirAsync(objetivo, 1, CancellationToken.None);

        var paso = Assert.Single(plan.Pasos.Where(p =>
            string.Equals(p.CodigoHerramienta, "GatewayConnectorTool", StringComparison.OrdinalIgnoreCase)));
        using var doc = JsonDocument.Parse(paso.Entrada!);
        Assert.Equal("API_DEMO", doc.RootElement.GetProperty("conector").GetString());
    }

    [Fact]
    public async Task MencionMandaSobreElSimilar()
    {
        // Dos conectores activos: el mencionado por código gana aunque otro sea
        // más similar por embeddings.
        const string objetivo = "usa el conector SMTP_DEMO para avisar";
        var firmaSmtp = "SMTP_DEMO SMTP demo Correo de salida SMTP";
        var firmaApi = "API_DEMO API demo Servicio interno de estado REST";
        var vSmtp = new float[] { 1f, 0f, 0f };
        var vApi = new float[] { 0.9f, 0.1f, 0f };
        var embeddings = new EmbeddingsPorEje()
            .Vector(objetivo, vApi)
            .Vector(firmaSmtp, vSmtp)
            .Vector(firmaApi, vApi);
        var smtp = ConectorDemo("SMTP_DEMO", "SMTP demo", "Correo de salida", "SMTP");
        var builder = Crear(RepoConectores(ConectorDemo(), smtp), OllamaCon(false), embeddings);

        var plan = await builder.ConstruirAsync(objetivo, 1, CancellationToken.None);

        var paso = Assert.Single(plan.Pasos.Where(p =>
            string.Equals(p.CodigoHerramienta, "GatewayConnectorTool", StringComparison.OrdinalIgnoreCase)));
        using var doc = JsonDocument.Parse(paso.Entrada!);
        Assert.Equal("SMTP_DEMO", doc.RootElement.GetProperty("conector").GetString());
    }

    [Fact]
    public async Task VariasMenciones_UnPasoPorConector()
    {
        // El caso de aceptación pide CRM + SharePoint + correo en el mismo plan.
        var crm = ConectorDemo("CRM_HUBSPOT", "CRM HubSpot", "Clientes del CRM", "REST");
        var sp = ConectorDemo("SP_STUB", "SharePoint demo", "Bibliotecas de documentos", "SharePoint");
        var smtp = ConectorDemo("SMTP_DEMO", "SMTP demo", "Correo de salida", "SMTP");
        var builder = Crear(RepoConectores(crm, sp, smtp), OllamaCon(false), null);

        var plan = await builder.ConstruirAsync(
            "consulta el CRM_HUBSPOT, busca en SP_STUB y avisa por SMTP_DEMO", 1, CancellationToken.None);

        var pasos = plan.Pasos.Where(p =>
            string.Equals(p.CodigoHerramienta, "GatewayConnectorTool", StringComparison.OrdinalIgnoreCase)).ToList();
        Assert.Equal(3, pasos.Count);
        var codigos = pasos.Select(p =>
            JsonDocument.Parse(p.Entrada!).RootElement.GetProperty("conector").GetString()).ToList();
        Assert.Contains("CRM_HUBSPOT", codigos);
        Assert.Contains("SP_STUB", codigos);
        Assert.Contains("SMTP_DEMO", codigos);
        // Los tres alimentan la entrega (dependencias por código de herramienta).
        var entrega = plan.Pasos.First(p => p.Nombre.Contains("Entregar"));
        foreach (var paso in pasos)
            Assert.Contains(plan.Dependencias, d =>
                d.StepOrigen == paso.Orden && d.StepDestino == entrega.Orden);
    }

    [Fact]
    public void EntradaConector_LlevaSoloCodigoYOperacion()
    {
        using var doc = JsonDocument.Parse(PlanBuilder.EntradaConector("X", "haz algo"));

        Assert.Equal("X", doc.RootElement.GetProperty("conector").GetString());
        Assert.Contains("haz algo", doc.RootElement.GetProperty("operacion").GetString());
        Assert.False(doc.RootElement.TryGetProperty("recurso", out _));
        Assert.False(doc.RootElement.TryGetProperty("metodo", out _));
    }

    [Fact]
    public void EntradaConector_MetodoSoloDelSetCerrado()
    {
        using var post = JsonDocument.Parse(PlanBuilder.EntradaConector("X", "prueba el verbo post", "POST"));
        Assert.Equal("POST", post.RootElement.GetProperty("metodo").GetString());

        // Cualquier otro valor se ignora: manda el defecto del conector.
        using var raro = JsonDocument.Parse(PlanBuilder.EntradaConector("X", "haz algo", "PURGE"));
        Assert.False(raro.RootElement.TryGetProperty("metodo", out _));
        using var nulo = JsonDocument.Parse(PlanBuilder.EntradaConector("X", "haz algo", null));
        Assert.False(nulo.RootElement.TryGetProperty("metodo", out _));
    }

    [Fact]
    public async Task VerboEscritura_ExigeAprobacionDeterminista()
    {
        // Gobierno: un paso conector con verbo de escritura (POST/PUT/PATCH/
        // DELETE) exige aprobación humana sin depender del flag del LLM.
        var builder = Crear(RepoConectores(ConectorDemo()), OllamaCon(false), null);

        var plan = await builder.ConstruirAsync(
            "crea con el verbo post usando el conector API_DEMO", 1, CancellationToken.None);

        Assert.True(plan.RequiereAprobacion);
        Assert.Contains(plan.Pasos, p => p.Tipo == "Approval");
    }

    [Fact]
    public async Task SoloLectura_NoExigeAprobacion()
    {
        var builder = Crear(RepoConectores(ConectorDemo()), OllamaCon(false), null);

        var plan = await builder.ConstruirAsync(
            "consulta con el verbo get usando el conector API_DEMO", 1, CancellationToken.None);

        Assert.False(plan.RequiereAprobacion);
        Assert.DoesNotContain(plan.Pasos, p => p.Tipo == "Approval");
    }

    [Fact]
    public void DetectarVerboMencionado_SoloPalabraCompletaDelSet()
    {
        Assert.Equal("POST", PlanBuilder.DetectarVerboMencionado("prueba el verbo post usando HTTPBIN"));
        Assert.Equal("DELETE", PlanBuilder.DetectarVerboMencionado("elimina con delete el registro"));
        Assert.Null(PlanBuilder.DetectarVerboMencionado("cuentame del postre de hoy"));
        Assert.Null(PlanBuilder.DetectarVerboMencionado("consulta el estado"));
        Assert.Null(PlanBuilder.DetectarVerboMencionado(null));
    }

    [Fact]
    public void ExtraerConectorOperacionYMetodo_LeeElContrato()
    {
        var (codigo, operacion, metodo) = AgentOrchestrator.ExtraerConectorOperacionYMetodo(
            """{"conector":"HTTPBIN","operacion":"probar post","metodo":"POST"}""");

        Assert.Equal("HTTPBIN", codigo);
        Assert.Equal("probar post", operacion);
        Assert.Equal("POST", metodo);
    }
}
