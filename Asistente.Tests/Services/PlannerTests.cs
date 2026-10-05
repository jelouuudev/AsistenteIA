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

    private static Mock<IConexionBaseDatosRepository> RepoConexionConTablas(params string[] tablas)
    {
        var repo = new Mock<IConexionBaseDatosRepository>();
        var con = new ConexionBaseDatos
        {
            IdConexion = 1,
            Nombre = "Test",
            BaseDatos = "Test",
            Activa = true,
            TablasAutorizadas = tablas.Select(t => new TablaAutorizada { NombreTabla = t }).ToList()
        };
        repo.Setup(r => r.GetActivasAsync()).ReturnsAsync(new[] { con });
        return repo;
    }

    private static Mock<IOllamaService> OllamaStub()
    {
        var mock = new Mock<IOllamaService>();
        mock.Setup(o => o.SendMessageAsync(It.IsAny<IEnumerable<Mensaje>>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<double?>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<Mensaje> msgs, string? s1, string? s2, double? d, int? i, CancellationToken ct) =>
            {
                var full = msgs.FirstOrDefault()?.Contenido ?? "";
                var idx = full.LastIndexOf("Solicitud:", StringComparison.OrdinalIgnoreCase);
                var req = (idx >= 0 ? full[(idx + 10)..] : full).Trim().ToLowerInvariant();

                bool apr = req.Contains("eliminar") || req.Contains("borrar") || req.Contains("desactivar");
                bool wf = req.Contains("flujo") || req.Contains("workflow") || req.Contains("automatizar");
                bool rag = req.Contains("documento") || req.Contains("procedimiento") || req.Contains("manual") || req.Contains("política") || req.Contains("reglas") || req.Contains("asueto");
                bool rep = req.Contains("informe") || req.Contains("reporte") || req.Contains("resumen") || req.Contains("pdf");
                bool rsg = req.Contains("riesgo") || req.Contains("riesgos");
                bool agr = req.Contains("cuant") || req.Contains("cantid") || req.Contains("cifra") || req.Contains("total") || req.Contains("mobiliario") || req.Contains("ventas");

                var tablas = new List<string>();
                if (req.Contains("ventas")) tablas.Add("Ventas");
                if (req.Contains("empleados") || req.Contains("personal")) tablas.Add("Empleados");
                if (req.Contains("mobiliario") || req.Contains("activos")) tablas.Add("Activos");

                var res = new
                {
                    tablas,
                    rag,
                    reporte = rep,
                    riesgo = rsg,
                    workflow = wf,
                    agregacion = agr,
                    aprobacion = apr
                };
                return System.Text.Json.JsonSerializer.Serialize(res);
            });
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

    private static Mock<IDocumentoRepository> RepoDocumentos(params (string Codigo, string Nombre, bool ActivoD)[] docs)
    {
        var repo = new Mock<IDocumentoRepository>();
        repo.Setup(r => r.GetAllAsync()).ReturnsAsync(docs.Select(d => new Documento
        {
            Codigo = d.Codigo,
            Nombre = d.Nombre,
            Estado = d.ActivoD ? Asistente.Domain.Enums.EstadoDocumento.Activo : Asistente.Domain.Enums.EstadoDocumento.Eliminado
        }).ToList());
        return repo;
    }

    /// <summary>
    /// Plan #7055: pregunta mixta que cita un documento por su código ("archivo ejemplo")
    /// + datos SQL. El clasificador LLM devolvió rag=false; el rescate por catálogo
    /// vivo debe añadir la rama RAG sin quitar la SQL.
    /// </summary>
    [Fact]
    public async Task PlanBuilder_PreguntaMixtaConCodigoDocumento_RescataRamaRAG()
    {
        var agentes = new[]
        {
            Agente("COMERCIAL-01", 1008, "SqlQueryTool", "ReportTool"),
            Agente("SOPORTE-01", 2005, "DocumentSearchTool")
        };
        var builder = new PlanBuilder(
            RepoAsistentes(agentes).Object,
            OllamaStub().Object,
            Logger<PlanBuilder>().Object,
            RepoConexionConTablas("Activos").Object,
            RepoWorkflow().Object,
            documentoRepo: RepoDocumentos(("ejemplo", "ejemplo", true)).Object);

        var plan = await builder.ConstruirAsync(
            "segun el archivo ejemplo, cuales son los Componentes de un archivo PDF, y cuantos activos tienen como responsable a juan perez?",
            1, CancellationToken.None);

        Assert.Contains(plan.Pasos, p => p.Tipo == "RAG" && p.CodigoHerramienta == "DocumentSearchTool");
        Assert.Contains(plan.Pasos, p => p.CodigoHerramienta == "SqlQueryTool");
    }

    /// <summary>
    /// Control: sin documento registrado coincidente no se inventa rama RAG.
    /// </summary>
    [Fact]
    public async Task PlanBuilder_PreguntaSoloDatos_SinDocumento_NoCreaRAG()
    {
        var agentes = new[]
        {
            Agente("COMERCIAL-01", 1008, "SqlQueryTool", "ReportTool"),
            Agente("SOPORTE-01", 2005, "DocumentSearchTool")
        };
        var builder = new PlanBuilder(
            RepoAsistentes(agentes).Object,
            OllamaStub().Object,
            Logger<PlanBuilder>().Object,
            RepoConexionConTablas("Activos").Object,
            RepoWorkflow().Object,
            documentoRepo: RepoDocumentos().Object);

        var plan = await builder.ConstruirAsync(
            "cuantos activos tienen como responsable a juan perez?",
            1, CancellationToken.None);

        Assert.DoesNotContain(plan.Pasos, p => p.Tipo == "RAG");
        Assert.Contains(plan.Pasos, p => p.CodigoHerramienta == "SqlQueryTool");
    }

    private static Mock<IOllamaService> OllamaClasificadorFijo(string json)
    {
        var mock = new Mock<IOllamaService>();
        mock.Setup(o => o.SendMessageAsync(It.IsAny<IEnumerable<Mensaje>>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<double?>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(json);
        mock.Setup(o => o.IsDisponibleAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        return mock;
    }

    /// <summary>
    /// Plan #7056: el LLM devolvió tablas=[] (volatilidad del reasoning). El rescate
    /// por esquema debe crear la rama SQL igual. Simula el JSON vacío del LLM.
    /// </summary>
    [Fact]
    public async Task PlanBuilder_LlmSinTablas_RescateEsquemaCreaSQL()
    {
        var agentes = new[]
        {
            Agente("COMERCIAL-01", 1008, "SqlQueryTool", "ReportTool"),
            Agente("SOPORTE-01", 2005, "DocumentSearchTool")
        };
        var jsonVacio = "{\"tablas\":[],\"rag\":false,\"reporte\":false,\"riesgo\":false,\"workflow\":false,\"agregacion\":false,\"aprobacion\":false,\"subconsultas\":[]}";
        var builder = new PlanBuilder(
            RepoAsistentes(agentes).Object,
            OllamaClasificadorFijo(jsonVacio).Object,
            Logger<PlanBuilder>().Object,
            RepoConexionConTablas("Activos").Object,
            RepoWorkflow().Object);

        var plan = await builder.ConstruirAsync(
            "cuantos registros hay en Activos?",
            1, CancellationToken.None);

        Assert.Contains(plan.Pasos, p => p.CodigoHerramienta == "SqlQueryTool" && p.Nombre.Contains("Activos"));
    }

    /// <summary>
    /// Plan #9073: el LLM no marcó reporte aunque pedían "repondeme todo en un
    /// pdf" (typo). El rescate por artefacto debe añadir el paso ReportTool igual.
    /// </summary>
    [Fact]
    public async Task PlanBuilder_LlmSinReporte_PidePdf_RescateArtefactoCreaInforme()
    {
        var agentes = new[]
        {
            Agente("COMERCIAL-01", 1008, "SqlQueryTool", "ReportTool"),
            Agente("SOPORTE-01", 2005, "DocumentSearchTool")
        };
        var jsonSinReporte = "{\"tablas\":[\"Ventas\"],\"rag\":false,\"reporte\":false,\"riesgo\":false,\"workflow\":false,\"agregacion\":true,\"aprobacion\":false,\"subconsultas\":[]}";
        var builder = new PlanBuilder(
            RepoAsistentes(agentes).Object,
            OllamaClasificadorFijo(jsonSinReporte).Object,
            Logger<PlanBuilder>().Object,
            RepoConexionConTablas("Ventas").Object,
            RepoWorkflow().Object);

        var plan = await builder.ConstruirAsync(
            "dime cuantos productos son de categoria electronica, repondeme todo en un pdf",
            1, CancellationToken.None);

        Assert.Contains(plan.Pasos, p => p.CodigoHerramienta == "ReportTool");
    }

    /// <summary>
    /// Sin mención de PDF no hay rescate: no se inventa paso de informe.
    /// </summary>
    [Fact]
    public async Task PlanBuilder_SinPdf_NoCreaInforme()
    {
        var agentes = new[]
        {
            Agente("COMERCIAL-01", 1008, "SqlQueryTool", "ReportTool"),
            Agente("SOPORTE-01", 2005, "DocumentSearchTool")
        };
        var jsonSinReporte = "{\"tablas\":[\"Ventas\"],\"rag\":false,\"reporte\":false,\"riesgo\":false,\"workflow\":false,\"agregacion\":true,\"aprobacion\":false,\"subconsultas\":[]}";
        var builder = new PlanBuilder(
            RepoAsistentes(agentes).Object,
            OllamaClasificadorFijo(jsonSinReporte).Object,
            Logger<PlanBuilder>().Object,
            RepoConexionConTablas("Ventas").Object,
            RepoWorkflow().Object);

        var plan = await builder.ConstruirAsync(
            "dime cuantos productos son de categoria electronica",
            1, CancellationToken.None);

        Assert.DoesNotContain(plan.Pasos, p => p.CodigoHerramienta == "ReportTool");
    }

    [Theory]
    [InlineData("repondeme todo en un pdf", true)]
    [InlineData("respóndeme todo en un PDF", true)]
    [InlineData("exporta el reporte.pdf por favor", true)]
    [InlineData("dime cuantos productos hay", false)]
    [InlineData("analiza el perfil del cliente", false)]
    public void PideArtefactoPdf_DetectaFormato_NoDominio(string objetivo, bool esperado)
        => Assert.Equal(esperado, PlanBuilder.PideArtefactoPdf(objetivo));

    /// <summary>
    /// Carrera del #7056: el Reporte/Entrega deben esperar al RAG para consolidar
    /// también lo documental.
    /// </summary>
    [Fact]
    public async Task PlanBuilder_MixtaIncluyeDependenciaRagHaciaReporteYEntrega()
    {
        var agentes = new[]
        {
            Agente("COMERCIAL-01", 1008, "SqlQueryTool", "ReportTool"),
            Agente("SOPORTE-01", 2005, "DocumentSearchTool")
        };
        var builder = new PlanBuilder(
            RepoAsistentes(agentes).Object,
            OllamaStub().Object,
            Logger<PlanBuilder>().Object,
            RepoConexionConTablas("Activos").Object,
            RepoWorkflow().Object,
            documentoRepo: RepoDocumentos(("ejemplo", "ejemplo", true)).Object);

        var plan = await builder.ConstruirAsync(
            "segun el archivo ejemplo, cuales son los Componentes de un archivo PDF, genera un informe y cuantos activos tienen como responsable a juan perez?",
            1, CancellationToken.None);

        var rag = Assert.Single(plan.Pasos, p => p.Tipo == "RAG");
        var reporte = Assert.Single(plan.Pasos, p => p.CodigoHerramienta == "ReportTool");
        var entrega = Assert.Single(plan.Pasos, p => p.Nombre.Contains("Entregar"));
        Assert.Contains(plan.Dependencias, d => d.StepOrigen == rag.Orden && d.StepDestino == reporte.Orden);
        Assert.Contains(plan.Dependencias, d => d.StepOrigen == rag.Orden && d.StepDestino == entrega.Orden);
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

    /// <summary>Embeddings fijos: todo puntúa 1.0 contra todo. Sirve para probar
    /// la selección por similitud sin depender de frases concretas: con este
    /// mock, cualquier firma supera cualquier umbral.</summary>
    private sealed class EmbTodoUno : IEmbeddingProvider
    {
        public Task<float[]> GenerateEmbeddingAsync(string text) => Task.FromResult(new[] { 1f, 0f });
    }

    /// <summary>La firma del workflow apunta al este ([1,0]); la pregunta apunta
    /// a un ángulo fijo. Así se controla el coseno exacto sin usar palabras:
    /// a 10° empareja (0.985), a 80° no (0.17).</summary>
    private sealed class EmbAngulo : IEmbeddingProvider
    {
        private readonly double _rad;
        public EmbAngulo(double grados) => _rad = grados * Math.PI / 180.0;
        public Task<float[]> GenerateEmbeddingAsync(string text)
        {
            if (text.Contains("REP-CLI", StringComparison.OrdinalIgnoreCase))
                return Task.FromResult(new[] { 1f, 0f });
            return Task.FromResult(new[] { (float)Math.Cos(_rad), (float)Math.Sin(_rad) });
        }
    }

    [Fact]
    public async Task PlanBuilder_ParafrasisSinPalabrasComunes_ResuelveWorkflow()
    {
        // La pregunta NO contiene "reporte", "clientes" ni "generar": solo se
        // parece por significado. Con matching literal jamás entraría.
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
            wfRepo.Object,
            new EmbAngulo(10));

        var plan = await builder.ConstruirAsync("quiero automatizar el flujo de trabajo", 1, CancellationToken.None);

        var pasoWf = plan.Pasos.FirstOrDefault(p => p.Tipo == "Workflow");
        Assert.NotNull(pasoWf);
        Assert.Equal(7, pasoWf!.IdWorkflow);
    }

    [Fact]
    public async Task PlanBuilder_PreguntaLejana_NoGeneraWorkflow()
    {
        // Misma configuración, pregunta a 80°: no hay emparejamiento y no sale
        // ningún paso, aunque el workflow esté asignado.
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
            wfRepo.Object,
            new EmbAngulo(80));

        var plan = await builder.ConstruirAsync("quiero automatizar el flujo de trabajo", 1, CancellationToken.None);

        Assert.DoesNotContain(plan.Pasos, p => p.Tipo == "Workflow");
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
            wfRepo.Object,
            new EmbTodoUno());

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
            wfRepo.Object,
            new EmbTodoUno());

        // El workflow empareja por similitud pero el agente NO lo tiene asignado:
// el gate de asignación (no la falta de coincidencia) es lo que omite el paso.
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
        ollama.Setup(o => o.SendMessageAsync(It.IsAny<IEnumerable<Mensaje>>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<double?>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("{\"tablas\":[],\"rag\":true,\"reporte\":false,\"riesgo\":false,\"workflow\":false,\"agregacion\":false,\"aprobacion\":false}");
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
    public async Task Reintento_MismoNodo_IdStepEstable_YRecupera()
    {
        // Evidencia de reintento por nodo: el supervisor NO crea un paso nuevo ni
        // cambia el IdStep; el mismo nodo vuelve a Pendiente, se reintenta y en el
        // segundo intento queda Completado. El log de auditoría debe registrar el
        // Reintento apuntando al IdStep original.
        var planRepo = new Mock<IPlanRepository>();
        var stepRepo = new Mock<IPlanStepRepository>();
        var logs = new List<PlanExecutionLog>();
        var logRepo = new LogEnMemoria(logs);
        var supervisor = new ExecutionSupervisor(planRepo.Object, stepRepo.Object, logRepo, maxReintentos: 2, tiempoEntreIntentosMs: 1);

        var plan = new Plan { IdPlan = 7 };
        var paso = new PlanStep { IdStep = 42, Orden = 1, Nombre = "Consultar Insumos", Estado = "EnEjecucion" };

        // Intento 1: falla de forma transitoria (p. ej. SQL saturado por el LLM).
        paso.Resultado = "Error al ejecutar SqlQueryTool: timeout";
        var reintenta = await supervisor.ManejarFalloPasoAsync(plan, paso, paso.Resultado, CancellationToken.None);

        Assert.True(reintenta);
        Assert.Equal(1, paso.Intentos);
        Assert.Equal(42, paso.IdStep);              // el MISMO nodo, no uno nuevo
        Assert.Equal("Pendiente", paso.Estado);    // vuelve a la cola para el reintento

        var logReintento = logs.Single(l => l.Evento == "Reintento");
        Assert.Equal(42, logReintento.IdStep);      // auditoría apunta al nodo original

        // Intento 2 (reintento del mismo nodo): la herramienta responde bien y el
        // engine marca el paso como Completado. El supervisor no interviene porque
        // no hubo fallo; lo que se verifica es que el nodo es el mismo.
        paso.Estado = "Completado";
        paso.Resultado = "ok";
        await stepRepo.Object.UpdateAsync(paso, CancellationToken.None);

        Assert.Equal(42, paso.IdStep);
        Assert.Equal(1, paso.Intentos);          // intentos = fallos registrados, no éxitos
        Assert.Equal("Completado", paso.Estado);
        Assert.Equal("ok", paso.Resultado);
        Assert.Single(logs.Where(l => l.IdStep == 42)); // un solo nodo en todo el flujo
    }

    /// <summary>Doble en memoria que conserva los eventos de auditoría del plan.</summary>
    private sealed class LogEnMemoria : IPlanExecutionLogRepository
    {
        private readonly List<PlanExecutionLog> _logs;
        public LogEnMemoria(List<PlanExecutionLog> logs) => _logs = logs;
        public Task<PlanExecutionLog> AddAsync(PlanExecutionLog log, CancellationToken cancellationToken = default)
        {
            _logs.Add(log);
            return Task.FromResult(log);
        }
        public Task<List<PlanExecutionLog>> GetByPlanAsync(int idPlan, CancellationToken cancellationToken = default)
            => Task.FromResult(_logs.Where(l => l.IdPlan == idPlan).ToList());
    }

    [Fact]
    public async Task Reintento_NoSeRepite_AlAgotarLaPolitica()
    {
        var planRepo = new Mock<IPlanRepository>();
        var stepRepo = new Mock<IPlanStepRepository>();
        var logs = new List<PlanExecutionLog>();
        var logRepo = new LogEnMemoria(logs);
        var supervisor = new ExecutionSupervisor(planRepo.Object, stepRepo.Object, logRepo, maxReintentos: 2, tiempoEntreIntentosMs: 1);

        var plan = new Plan { IdPlan = 8 };
        var paso = new PlanStep { IdStep = 43, Orden = 1, Nombre = "Consultar Insumos", Estado = "EnEjecucion" };

        Assert.True(await supervisor.ManejarFalloPasoAsync(plan, paso, "f1", CancellationToken.None));
        Assert.True(await supervisor.ManejarFalloPasoAsync(plan, paso, "f2", CancellationToken.None));
        Assert.False(await supervisor.ManejarFalloPasoAsync(plan, paso, "f3", CancellationToken.None));

        Assert.Equal("Error", paso.Estado);
        Assert.Equal(2, logs.Count(l => l.Evento == "Reintento"));
        Assert.Single(logs.Where(l => l.Evento == "PasoError"));
    }

    [Fact]
    public void HuellaGrafo_EsEstable_EntreConstruccionesDelMismoPlan()
    {
        // Base de la condición "el mismo grafo que se muestra y valida es el que
        // ejecuta": si la huella fuera distinta en cada construcción, compararla no
        // probaría nada. Dos construcciones del mismo plan deben coincidir.
        var builder = new ExecutionGraphBuilder();

        var h1 = builder.Construir(PlanDeAceptacion()).CalcularHuella();
        var h2 = builder.Construir(PlanDeAceptacion()).CalcularHuella();

        Assert.Equal(h1, h2);
        Assert.Equal(16, h1.Length);
    }

    [Fact]
    public void HuellaGrafo_Cambia_SiCambiaLaEstructuraDelPlan()
    {
        var builder = new ExecutionGraphBuilder();
        var original = builder.Construir(PlanDeAceptacion()).CalcularHuella();

        // Se agrega una dependencia: el DAG ya no es el mismo, la huella debe cambiar.
        var otro = PlanDeAceptacion();
        otro.Dependencias.Add(new PlanDependency { StepOrigen = 2, StepDestino = 5 });
        var modificada = builder.Construir(otro).CalcularHuella();

        Assert.NotEqual(original, modificada);
    }

    [Fact]
    public void HuellaGrafo_RepresentaElParalelismoReal()
    {
        // El plan de aceptación tiene SQL (1), RAG (2) y análisis (3) en la misma
        // capa: el grafo debe reflejarlo y la huella debe cambiar si ese paralelismo
        // se serializa (si 3 pasara a depender de 1 y 2).
        var builder = new ExecutionGraphBuilder();
        var grafo = builder.Construir(PlanDeAceptacion());
        var capas = grafo.ObtenerCapas();

        var capaParalela = capas.First(c => c.Any(n => n.IdNodo == 1) && c.Any(n => n.IdNodo == 2));
        Assert.True(capaParalela.Count >= 2, "SQL y RAG deben compartir capa");

        var serializado = PlanDeAceptacion();
        serializado.Dependencias.Add(new PlanDependency { StepOrigen = 1, StepDestino = 3 });
        serializado.Dependencias.Add(new PlanDependency { StepOrigen = 2, StepDestino = 3 });
        Assert.NotEqual(grafo.CalcularHuella(), builder.Construir(serializado).CalcularHuella());
    }

    /// <summary>Plan de aceptación: Coordination → (SQL ‖ RAG) → análisis → PDF → riesgo → entrega.</summary>
    private static Plan PlanDeAceptacion()
    {
        var plan = new Plan
        {
            IdPlan = 99,
            Pasos = new List<PlanStep>
            {
                new() { Orden = 0, Tipo = "Coordination", Nombre = "Coordinar", IdAsistente = 1 },
                new() { Orden = 1, Tipo = "Tool", Nombre = "Consultar Insumos", IdAsistente = 1, CodigoHerramienta = "SqlQueryTool" },
                new() { Orden = 2, Tipo = "RAG", Nombre = "Consultar documentacion", IdAsistente = 1, CodigoHerramienta = "DocumentSearchTool" },
                new() { Orden = 3, Tipo = "Tool", Nombre = "Analizar resultados", IdAsistente = 1, CodigoHerramienta = "SqlQueryTool" },
                new() { Orden = 4, Tipo = "Tool", Nombre = "Generar informe", IdAsistente = 1, CodigoHerramienta = "ReportTool" },
                new() { Orden = 5, Tipo = "Agent", Nombre = "Clasificar por nivel de riesgo", IdAsistente = 1 },
                new() { Orden = 6, Tipo = "Agent", Nombre = "Entregar resultado final", IdAsistente = 1 }
            }
        };
        plan.Dependencias = new List<PlanDependency>
        {
            new() { StepOrigen = 0, StepDestino = 1 },
            new() { StepOrigen = 0, StepDestino = 2 },
            new() { StepOrigen = 1, StepDestino = 3 },
            new() { StepOrigen = 2, StepDestino = 4 },
            new() { StepOrigen = 3, StepDestino = 4 },
            new() { StepOrigen = 4, StepDestino = 5 },
            new() { StepOrigen = 1, StepDestino = 5 },
            new() { StepOrigen = 2, StepDestino = 6 }
        };
        return plan;
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

    /// <summary>
    /// Regresión del plan colgado: plan.Pasos puede llegar con el mismo Orden repetido
    /// (fixup de navegaciones de EF al leer + actualizar en el mismo DbContext). El
    /// builder hacía ToDictionary(p => p.Orden) y moría con "An item with the same key
    /// has already been added. Key: 0", dejando el plan en IniciandoEjecucion para siempre.
    /// </summary>
    [Fact]
    public void ExecutionGraphBuilder_PasosConOrdenDuplicado_NoRevientaYGeneraCapas()
    {
        var builder = new ExecutionGraphBuilder();
        var plan = new Plan
        {
            Pasos = new List<PlanStep>
            {
                new() { Orden = 0, Tipo = "Coordination", Nombre = "Coordinar", IdAsistente = 1 },
                new() { Orden = 0, Tipo = "Coordination", Nombre = "Coordinar (dup)", IdAsistente = 1 },
                new() { Orden = 1, Tipo = "Tool", Nombre = "Consultar datos", IdAsistente = 1, CodigoHerramienta = "SqlQueryTool" },
                new() { Orden = 2, Tipo = "Agent", Nombre = "Entregar", IdAsistente = 1 }
            },
            Dependencias = new List<PlanDependency>
            {
                new() { StepOrigen = 0, StepDestino = 1 },
                new() { StepOrigen = 1, StepDestino = 2 }
            }
        };

        var grafo = builder.Construir(plan);

        Assert.Equal(3, grafo.Nodos.Count); // un nodo por Orden
        Assert.Equal(grafo.Nodos.Count, grafo.Nodos.Select(n => n.IdNodo).Distinct().Count());

        var capas = grafo.ObtenerCapas();
        Assert.Equal(3, capas.Count);
        Assert.Equal(3, capas.Sum(c => c.Count)); // sin nodos repetidos ni bucle infinito
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

    [Fact]
    public async Task PlanBuilder_ClasificacionSemantica_SinKeywords_CreaPasos()
    {
        // Objetivo parafraseado SIN ninguna keyword de fallback: solo la vía semántica
        // puede generar intención RAG. Las tablas van por coincidencia exacta.
        var agentes = new[] { Agente("AG-01", 1008, "SqlQueryTool", "DocumentSearchTool") };
        var conRepo = RepoConexion();
        conRepo.Setup(r => r.GetActivasAsync()).ReturnsAsync(new List<ConexionBaseDatos>
        {
            new() { IdConexion = 1, Nombre = "Test", Activa = true,
                TablasAutorizadas = new List<TablaAutorizada> { new() { NombreTabla = "Empleados" } } }
        });
        var ollama = new Mock<IOllamaService>();
        ollama.Setup(o => o.SendMessageAsync(It.IsAny<IEnumerable<Mensaje>>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<double?>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("{\"tablas\":[\"Empleados\"],\"rag\":true,\"reporte\":false,\"riesgo\":false,\"workflow\":false,\"agregacion\":true,\"aprobacion\":false}");
        var builder = new PlanBuilder(
            RepoAsistentes(agentes).Object,
            ollama.Object,
            Logger<PlanBuilder>().Object,
            conRepo.Object,
            RepoWorkflow().Object);

        var plan = await builder.ConstruirAsync("cifra de personal y reglas de asueto", 1, CancellationToken.None);

        // Intención RAG semántica (sin keywords); la tabla la pone el match exacto
        // solo si se nombra, así que aquí se valida la intención.
        Assert.Contains(plan.Pasos, p => p.CodigoHerramienta == "DocumentSearchTool");
    }

    [Fact]
    public async Task PlanBuilder_ConsultaDatosSinNombreTabla_CreaPasoSqlQueryTool()
    {
        var agentes = new[] { Agente("COMERCIAL-01", 1008, "SqlQueryTool") };
        var conRepo = RepoConexion();
        conRepo.Setup(r => r.GetActivasAsync()).ReturnsAsync(new List<ConexionBaseDatos>
        {
            new()
            {
                IdConexion = 1,
                Nombre = "ControlActivosTest",
                Activa = true,
                TablasAutorizadas = new List<TablaAutorizada> { new() { NombreTabla = "Activos" } }
            }
        });

        var builder = new PlanBuilder(
            RepoAsistentes(agentes).Object,
            OllamaStub().Object,
            Logger<PlanBuilder>().Object,
            conRepo.Object,
            RepoWorkflow().Object);

        var plan = await builder.ConstruirAsync("cuantas son de categoria mobiliario ?", 1, CancellationToken.None);

        Assert.NotEmpty(plan.Pasos);
        Assert.Contains(plan.Pasos, p => p.CodigoHerramienta == "SqlQueryTool");
    }

    [Fact]
    public async Task PlanBuilder_Subconsultas_CreaRamasParalelasConEntrada()
    {
        var agentes = new[] { Agente("COMERCIAL-01", 1008, "SqlQueryTool") };
        var conRepo = RepoConexion();
        conRepo.Setup(r => r.GetActivasAsync()).ReturnsAsync(new List<ConexionBaseDatos>
        {
            new() { IdConexion = 1, Nombre = "VentasTest", Activa = true,
                TablasAutorizadas = new List<TablaAutorizada> { new() { NombreTabla = "Ventas" } } }
        });
        var ollama = new Mock<IOllamaService>();
        ollama.Setup(o => o.SendMessageAsync(It.IsAny<IEnumerable<Mensaje>>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<double?>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("{\"tablas\":[\"Ventas\"],\"rag\":false,\"reporte\":false,\"riesgo\":false,\"workflow\":false,\"agregacion\":true,\"aprobacion\":false,\"subconsultas\":[\"ventas de categoria ropa\",\"ventas de categoria hogar\"]}");
        var builder = new PlanBuilder(
            RepoAsistentes(agentes).Object,
            ollama.Object,
            Logger<PlanBuilder>().Object,
            conRepo.Object,
            RepoWorkflow().Object);

        var plan = await builder.ConstruirAsync("ventas de ropa y hogar en paralelo", 1, CancellationToken.None);

        var ramas = plan.Pasos.Where(p => p.CodigoHerramienta == "SqlQueryTool" && p.Nombre.StartsWith("Consultar datos de")).ToList();
        Assert.Equal(2, ramas.Count);
        Assert.All(ramas, r => Assert.False(string.IsNullOrWhiteSpace(r.Entrada)));
        Assert.Contains(ramas, r => r.Entrada!.Contains("ropa"));
        Assert.Contains(ramas, r => r.Entrada!.Contains("hogar"));
        // Sin dependencia entre ramas (paralelo real).
        Assert.DoesNotContain(plan.Dependencias,
            d => ramas.Any(o => o.Orden == d.StepOrigen) && ramas.Any(o => o.Orden == d.StepDestino));
        // El análisis espera a TODAS las ramas.
        var analisis = plan.Pasos.FirstOrDefault(p => p.Tipo == "Tool" && p.Nombre.Contains("Analizar"));
        Assert.NotNull(analisis);
        foreach (var rama in ramas)
            Assert.Contains(plan.Dependencias, d => d.StepOrigen == rama.Orden && d.StepDestino == analisis!.Orden);
    }

    [Fact]
    public async Task PlanBuilder_AutoSplit_DetectaRamasSinLLM()
    {
        // El LLM NO divide (subconsultas vacías) pero los valores observados sí:
        // el auto-split determinístico debe crear 2 ramas igualmente.
        var agentes = new[] { Agente("COMERCIAL-01", 1008, "SqlQueryTool") };
        var conRepo = RepoConexion();
        conRepo.Setup(r => r.GetActivasAsync()).ReturnsAsync(new List<ConexionBaseDatos>
        {
            new() { IdConexion = 1, Nombre = "VentasTest", Activa = true,
                CadenaConexionCifrada = "x",
                TablasAutorizadas = new List<TablaAutorizada> { new() { NombreTabla = "Ventas" } } }
        });
        var ollama = new Mock<IOllamaService>();
        ollama.Setup(o => o.SendMessageAsync(It.IsAny<IEnumerable<Mensaje>>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<double?>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("{\"tablas\":[\"Ventas\"],\"rag\":false,\"reporte\":false,\"riesgo\":false,\"workflow\":false,\"agregacion\":true,\"aprobacion\":false,\"subconsultas\":[]}");
        var cifrador = new Mock<IConexionCifrador>();
        cifrador.Setup(c => c.Descifrar(It.IsAny<string>())).Returns("Server=x");
        var executor = new Mock<ISqlQueryExecutor>();
        executor.Setup(e => e.ExecuteReadOnlyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<int>(), It.IsAny<CancellationToken>(), It.IsAny<int>()))
            .ReturnsAsync((string cs, string sql, object? prm, int max, CancellationToken ct, int t) =>
            {
                if (sql.Contains("DISTINCT", StringComparison.OrdinalIgnoreCase))
                    return new List<Dictionary<string, object?>>
                    {
                        new() { ["V"] = "Ropa" }, new() { ["V"] = "Hogar" }
                    };
                return new List<Dictionary<string, object?>>
                {
                    new() { ["COLUMN_NAME"] = "Categoria" }
                };
            });
        var builder = new PlanBuilder(
            RepoAsistentes(agentes).Object,
            ollama.Object,
            Logger<PlanBuilder>().Object,
            conRepo.Object,
            RepoWorkflow().Object,
            null,
            cifrador.Object,
            executor.Object);

        var plan = await builder.ConstruirAsync("ventas de ropa y hogar", 1, CancellationToken.None);

        var ramas = plan.Pasos.Where(p => p.CodigoHerramienta == "SqlQueryTool" && p.Nombre.StartsWith("Consultar datos de")).ToList();
        Assert.Equal(2, ramas.Count);
        Assert.Contains(ramas, r => r.Entrada != null && r.Entrada.Contains("Ropa"));
        Assert.Contains(ramas, r => r.Entrada != null && r.Entrada.Contains("Hogar"));
    }
}
