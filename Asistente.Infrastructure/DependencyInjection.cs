using Asistente.Application.Interfaces;
using Asistente.Domain.Interfaces;
using Asistente.Infrastructure.Data;
using Asistente.Infrastructure.Repositories;
using Asistente.Infrastructure.Services;
using Asistente.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Quartz;

namespace Asistente.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        services.AddDbContext<AsistenteDbContext>(options =>
            options.UseSqlServer(connectionString));

        // Configuración de opciones (vector store / embeddings)
        services.Configure<ChromaConfig>(configuration.GetSection("Chroma"));
        services.Configure<EmbeddingConfig>(configuration.GetSection("Embedding"));

        // Servicios de IA en runtime (ETAPAs previas): almacén vectorial y embeddings
        // NOTA: OllamaEmbeddingProvider se registra como cliente tipado de IEmbeddingProvider
        // para que el HttpClient resuelva SIEMPRE con BaseAddress = Ollama.Url (evita el
        // error "An invalid request URI was provided" al llamar a /api/embeddings).
        services.AddHttpClient<ChromaVectorStore>((sp, client) =>
        {
            var cfg = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<ChromaConfig>>().Value;
            client.BaseAddress = new Uri(cfg.Url);
            client.Timeout = TimeSpan.FromSeconds((cfg.TimeoutSegundos > 0 ? cfg.TimeoutSegundos : 30) + 5);
        });
        services.AddHttpClient<IEmbeddingProvider, OllamaEmbeddingProvider>((sp, client) =>
        {
            var cfg = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<OllamaConfig>>().Value;
            client.BaseAddress = new Uri(cfg.Url);
            client.Timeout = TimeSpan.FromSeconds((cfg.TimeoutSegundos > 0 ? cfg.TimeoutSegundos : 3600) + 5);
        });
        services.AddSingleton<InMemoryVectorStore>();
        // NOTA: ChromaVectorStore se registra via AddHttpClient<ChromaVectorStore> (arriba) para
        // inyectar el HttpClient con BaseAddress = ChromaConfig.Url. NO registrar AddScoped<ChromaVectorStore>()
        // aqui, porque pisaria el AddHttpClient y el HttpClient quedaria sin BaseAddress (BaseAddress=NULL).
        services.AddScoped<IConfiguracionWorkflowRepository, ConfiguracionWorkflowRepository>();

        // Selección del almacén vectorial según configuración (ETAPA 15 - despliegue sin ChromaDB)
        services.AddScoped<IVectorStore>(sp =>
        {
            var embeddingCfg = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<EmbeddingConfig>>().Value;
            var baseVectorial = (embeddingCfg.BaseVectorial ?? "Chroma").Trim();
            if (baseVectorial.Equals("InMemory", StringComparison.OrdinalIgnoreCase))
                return sp.GetRequiredService<InMemoryVectorStore>();
            return sp.GetRequiredService<ChromaVectorStore>();
        });

        // Repositorios base
        services.AddScoped<IConversacionRepository, ConversacionRepository>();
        services.AddScoped<IMensajeRepository, MensajeRepository>();
        services.AddScoped<IUsuarioRepository, UsuarioRepository>();
        services.AddScoped<IRolRepository, RolRepository>();
        services.AddScoped<IAuditoriaRepository, AuditoriaRepository>();
        services.AddScoped<IPasswordHasher, PasswordHasher>();
        services.AddScoped<IAsistenteRepository, AsistenteRepository>();
        services.AddScoped<IPromptSistemaRepository, PromptSistemaRepository>();
        services.AddScoped<IHistorialPromptRepository, HistorialPromptRepository>();
        services.AddScoped<IConfiguracionMemoriaRepository, ConfiguracionMemoriaRepository>();

        // Documentos y procesamiento
        services.AddScoped<ICategoriaDocumentoRepository, CategoriaDocumentoRepository>();
        services.AddScoped<IDocumentoRepository, DocumentoRepository>();
        services.AddScoped<IDocumentoVersionRepository, DocumentoVersionRepository>();
        services.AddScoped<IAuditoriaDocumentalRepository, AuditoriaDocumentalRepository>();
        services.AddScoped<IFileStorageService, FileStorageService>();
        services.AddScoped<IProcesamientoDocumentalRepository, ProcesamientoDocumentalRepository>();
        services.AddScoped<IExtractoraTextoService, ExtractoraTextoService>();
        services.AddScoped<INormalizadorTextoService, NormalizadorTextoService>();
        services.AddScoped<IChunkingService, ChunkingService>();
        services.AddScoped<IDocumentoIndexadoRepository, DocumentoIndexadoRepository>();
        services.AddScoped<IEmbeddingConfiguracionRepository, EmbeddingConfiguracionRepository>();

        // RAG / Fuentes
        services.AddScoped<IFuenteConocimientoRepository, FuenteConocimientoRepository>();
        services.AddScoped<IAsistenteFuenteRepository, AsistenteFuenteRepository>();
        services.AddScoped<IDocumentoFuenteRepository, DocumentoFuenteRepository>();
        services.AddScoped<IConfiguracionRAGRepository, ConfiguracionRAGRepository>();
        services.AddScoped<IKnowledgeSource, ChromaDbKnowledgeSource>();

        // SQL / Motor de consultas
        services.AddScoped<IConexionBaseDatosRepository, ConexionBaseDatosRepository>();
        services.AddScoped<ITablaAutorizadaRepository, TablaAutorizadaRepository>();
        services.AddScoped<IVistaAutorizadaRepository, VistaAutorizadaRepository>();
        services.AddScoped<IConsultaEjecutadaRepository, ConsultaEjecutadaRepository>();
        services.AddScoped<IConsultaPlantillaRepository, ConsultaPlantillaRepository>();
        services.AddScoped<IConfiguracionMotorConsultasRepository, ConfiguracionMotorConsultasRepository>();
        services.AddScoped<IConexionCifrador, ConexionCifrador>();
        services.AddScoped<ISqlQueryExecutor, SqlQueryExecutor>();
        services.AddScoped<ISchemaDiscoveryService, SchemaDiscoveryService>();

        // Motor de Herramientas (Tool Orchestrator) - ETAPA 11
        services.AddScoped<IHerramientaRepository, HerramientaRepository>();
        services.AddScoped<IAsistenteHerramientaRepository, AsistenteHerramientaRepository>();
        services.AddScoped<IEjecucionHerramientaRepository, EjecucionHerramientaRepository>();
        services.AddScoped<IConfiguracionOrchestratorRepository, ConfiguracionOrchestratorRepository>();

        // Agent Orchestrator (ETAPA 17)
        services.AddScoped<IAgentExecutionRepository, AgentExecutionRepository>();
        services.AddScoped<IAgentExecutionStepRepository, AgentExecutionStepRepository>();
        services.AddScoped<IAgentExecutionTraceRepository, AgentExecutionTraceRepository>();
        services.AddScoped<IAgentCollaborationRuleRepository, AgentCollaborationRuleRepository>();

        // Planner Engine (ETAPA 18)
        services.AddScoped<IPlanRepository, PlanRepository>();
        services.AddScoped<IPlanStepRepository, PlanStepRepository>();
        services.AddScoped<IPlanDependencyRepository, PlanDependencyRepository>();
        services.AddScoped<IPlanExecutionLogRepository, PlanExecutionLogRepository>();

        // Centro de Aprobaciones / Human-in-the-Loop (ETAPA 19)
        services.AddScoped<IApprovalRequestRepository, ApprovalRequestRepository>();
        services.AddScoped<IApprovalDecisionRepository, ApprovalDecisionRepository>();
        services.AddScoped<IApprovalAssigneeRepository, ApprovalAssigneeRepository>();
        services.AddScoped<IApprovalPolicyRepository, ApprovalPolicyRepository>();

        // Workflow Engine (ETAPA 12)
        services.AddScoped<IWorkflowRepository, WorkflowRepository>();
        services.AddScoped<IWorkflowPasoRepository, WorkflowPasoRepository>();
        services.AddScoped<IWorkflowEjecucionRepository, WorkflowEjecucionRepository>();
        services.AddScoped<IWorkflowPasoEjecucionRepository, WorkflowPasoEjecucionRepository>();

        // Motor de Eventos Empresariales (ETAPA 13)
        services.AddScoped<IEventoEmpresarialRepository, EventoEmpresarialRepository>();
        services.AddScoped<IReglaEventoRepository, ReglaEventoRepository>();
        services.AddScoped<IEventoProcesadoRepository, EventoProcesadoRepository>();
        services.AddScoped<ITareaProgramadaRepository, TareaProgramadaRepository>();
        services.AddScoped<IConfiguracionEventoMotorRepository, ConfiguracionEventoMotorRepository>();
        services.AddScoped<IDisparadorEventoRepository, DisparadorEventoRepository>();

        // Seguridad, Gobierno, Auditoría y Observabilidad (ETAPA 14)
        services.AddScoped<IPermisoRepository, PermisoRepository>();
        services.AddScoped<IUsuarioAsistenteRepository, UsuarioAsistenteRepository>();
        services.AddScoped<IUsuarioFuenteRepository, UsuarioFuenteRepository>();
        services.AddScoped<IPoliticaIARepository, PoliticaIARepository>();
        services.AddScoped<IAuditoriaActividadRepository, AuditoriaActividadRepository>();
        services.AddScoped<IAuditoriaIARepository, AuditoriaIARepository>();
        services.AddScoped<IMetricasIARepository, MetricasIARepository>();

        services.AddScoped<IUnitOfWork>(sp =>
            sp.GetRequiredService<AsistenteDbContext>());

        // Autenticación / JWT
        services.Configure<JwtSettings>(configuration.GetSection("JwtSettings"));
        services.AddScoped<JwtTokenService>();
        services.AddScoped<Asistente.Application.Interfaces.IJwtTokenService>(sp =>
            sp.GetRequiredService<JwtTokenService>());

        // Ollama
        services.Configure<OllamaConfig>(configuration.GetSection("Ollama"));
        services.AddHttpClient<IOllamaService, OllamaService>((sp, client) =>
        {
            var config = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<OllamaConfig>>();
            client.BaseAddress = new Uri(config.Value.Url);
            client.Timeout = TimeSpan.FromSeconds(config.Value.TimeoutSegundos + 5);
        });

        // Quartz.NET (programación de tareas - ETAPA 13)
        services.AddQuartz(q =>
        {
            q.UseSimpleTypeLoader();
            q.UseInMemoryStore();
        });
        services.AddQuartzHostedService(options => options.WaitForJobsToComplete = true);

        // Background Services: pipeline documental ACTIVO (SQL disponible).
        // Procesamiento e indexación son los que convierten lo subido en chunks
        // y vectores para RAG; con ellos apagados DocumentoIndexado se queda en 0.
        services.AddHostedService<IndexacionBackgroundService>();
        services.AddHostedService<ProcesamientoDocumentalBackgroundService>();
        // Background Services desacoplados - TEMPORALMENTE DESHABILITADOS PARA PRUEBAS SIN SQL
        // services.AddHostedService<ProcesadorEventosBackgroundService>();
        // services.AddHostedService<ProgramadorTareasBackgroundService>();
        // services.AddHostedService<EvaluadorDisparadoresBackgroundService>();

        return services;
    }
}
