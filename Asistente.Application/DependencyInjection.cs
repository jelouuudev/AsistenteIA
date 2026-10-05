using Asistente.Application.Interfaces;
using Asistente.Application.Orchestrator;
using Asistente.Application.Aprobaciones;
using Asistente.Application.Orchestrator.Planner;
using Asistente.Application.Services;
using Asistente.Application.Services.Herramientas;
using Asistente.Application.Services.Workflows;
using Asistente.Application.Services.Eventos;
using Asistente.Application.Services.Seguridad;
using Asistente.Domain.Interfaces;
using Asistente.Shared;
using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Asistente.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IChatService, ChatService>();
        services.AddScoped<IUsuarioService, UsuarioService>();
        services.AddScoped<IRolService, RolService>();
        services.AddScoped<IAuditoriaService, AuditoriaService>();
        services.AddScoped<IMemoriaService, MemoriaService>();
        services.AddScoped<IConfiguracionMemoriaService, ConfiguracionMemoriaService>();
        services.AddScoped<ContextoService>();
        services.AddScoped<AsistenteService>();
        services.AddScoped<PromptSistemaService>();
        services.AddScoped<ICategoriaDocumentoService, CategoriaDocumentoService>();
        services.AddScoped<IDocumentoService, DocumentoService>();
        services.AddScoped<IProcesamientoDocumentalService, ProcesamientoDocumentalService>();
        services.AddScoped<IRecuperacionService, RecuperacionService>();
        services.AddScoped<IRagService, RagService>();
        services.AddScoped<IIndexacionService, IndexacionService>();
        services.AddScoped<IEmbeddingConfiguracionService, EmbeddingConfiguracionService>();
        services.AddScoped<EmbeddingService>();
        services.AddScoped<IFuenteConocimientoService, FuenteConocimientoService>();
        services.AddScoped<IConfiguracionRAGService, ConfiguracionRAGService>();
        services.AddScoped<IConexionBaseDatosService, ConexionBaseDatosService>();
        services.AddScoped<IConsultaPlantillaService, ConsultaPlantillaService>();
        services.AddScoped<IConsultaEjecutadaService, ConsultaEjecutadaService>();
        services.AddScoped<IConfiguracionMotorConsultasService, ConfiguracionMotorConsultasService>();
        services.AddScoped<IQueryEmpresarialService, QueryEmpresarialService>();

        // Motor de Herramientas (Tool Orchestrator) - ETAPA 11
        services.AddScoped<IHerramientaService, HerramientaService>();
        services.AddScoped<IEjecucionHerramientaService, EjecucionHerramientaService>();
        services.AddScoped<IDecisionHerramientaService, DecisionHerramientaService>();
        services.AddScoped<IToolOrchestrator, ToolOrchestrator>();
        // Selección de herramienta por similitud con la DESCRIPCIÓN de cada herramienta
        // (configuración en BD), en lugar de listas de términos.
        services.AddSingleton<ISeleccionHerramientaSemantica>(sp =>
            new SeleccionHerramientaSemantica(sp.GetService<IEmbeddingProvider>()));
        services.AddScoped<ITool, DocumentSearchTool>();
        services.AddScoped<ITool, SqlQueryTool>();
        services.AddScoped<ITool, CalculatorTool>();
        services.AddScoped<ITool, DateTimeTool>();
        services.AddScoped<ITool, ReportTool>();

        // Agent Orchestrator (ETAPA 17)
        services.AddScoped<IAgentSelector, AgentSelector>();
        services.AddScoped<IContextManager, ContextManager>();
        services.AddScoped<IResponseAggregator, ResponseAggregator>();
        services.AddScoped<IAgentOrchestrator, AgentOrchestrator>();
        // Lazy para romper la dependencia circular ChatService <-> IAgentOrchestrator
        services.AddScoped<Lazy<IAgentOrchestrator>>(sp => new Lazy<IAgentOrchestrator>(() => sp.GetRequiredService<IAgentOrchestrator>()));

        // Planner Engine (ETAPA 18)
        services.AddScoped<PlanBuilder>();
        services.AddScoped<PlanValidator>();
        services.AddScoped<ExecutionGraphBuilder>();
        services.AddScoped<ExecutionSupervisor>();
        services.AddScoped<IPlannerEngine, PlannerEngine>();

        // Centro de Aprobaciones / Human-in-the-Loop (ETAPA 19)
        services.AddScoped<ApprovalManager>();

        // Motor de Workflows (Workflow Engine) - ETAPA 12
        services.AddScoped<IWorkflowEngine, WorkflowEngine>();
        services.AddScoped<IWorkflowService, WorkflowService>();
        services.AddScoped<IConfiguracionWorkflowService, ConfiguracionWorkflowService>();
        services.AddScoped<IWorkflowDecisionService, WorkflowDecisionService>();

        // Motor de Eventos Empresariales (Event Motor) - ETAPA 13
        services.AddScoped<IEventoEmpresarialService, EventoEmpresarialService>();
        services.AddScoped<IReglaEventoService, ReglaEventoService>();
        services.AddScoped<IEventoProcesadoService, EventoProcesadoService>();
        services.AddScoped<ITareaProgramadaService, TareaProgramadaService>();
        services.AddScoped<IConfiguracionEventoMotorService, ConfiguracionEventoMotorService>();
        services.AddScoped<IEventoMotorService, EventoMotorService>();
        services.AddScoped<IDisparadorEventoService, DisparadorEventoService>();
        services.AddScoped<IMonitoreoEventosService, MonitoreoEventosService>();

        // Seguridad, Gobierno, Auditoría y Observabilidad (ETAPA 14)
        services.AddScoped<IPermisoService, PermisoService>();
        services.AddScoped<IAutorizacionService, AutorizacionService>();
        services.AddScoped<IPoliticaIAService, PoliticaIAService>();
        services.AddScoped<IProteccionDatosService, ProteccionDatosService>();
        services.AddScoped<IPromptInjectionService, PromptInjectionService>();
        services.AddSingleton<IRateLimitService, RateLimitService>();
        services.AddScoped<IDashboardSeguridadService, DashboardSeguridadService>();

        services.Configure<ProcesamientoConfig>(configuration.GetSection("ProcesamientoDocumental"));

        // Registrar validadores de FluentValidation
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

        return services;
    }
}
