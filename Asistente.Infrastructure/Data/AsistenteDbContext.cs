using Asistente.Domain.Entities;
using Asistente.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Asistente.Infrastructure.Data;

public class AsistenteDbContext : DbContext, IUnitOfWork
{
    public AsistenteDbContext(DbContextOptions<AsistenteDbContext> options) : base(options)
    {
    }

    public DbSet<Conversacion> Conversaciones => Set<Conversacion>();
    public DbSet<Mensaje> Mensajes => Set<Mensaje>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Rol> Roles => Set<Rol>();
    public DbSet<UsuarioRol> UsuarioRoles => Set<UsuarioRol>();
    public DbSet<AuditoriaSesion> AuditoriasSesion => Set<AuditoriaSesion>();
    public DbSet<AuditoriaActividad> AuditoriasActividad => Set<AuditoriaActividad>();
    public DbSet<Domain.Entities.Asistente> Asistentes => Set<Domain.Entities.Asistente>();
    public DbSet<PromptSistema> PromptsSistema => Set<PromptSistema>();
    public DbSet<HistorialPrompt> HistorialPrompts => Set<HistorialPrompt>();
    public DbSet<ConfiguracionMemoria> ConfiguracionesMemoria => Set<ConfiguracionMemoria>();
    public DbSet<CategoriaDocumento> CategoriasDocumento => Set<CategoriaDocumento>();
    public DbSet<Documento> Documentos => Set<Documento>();
    public DbSet<DocumentoVersion> DocumentoVersiones => Set<DocumentoVersion>();
    public DbSet<AuditoriaDocumental> AuditoriasDocumental => Set<AuditoriaDocumental>();
    public DbSet<DocumentoProcesado> DocumentosProcesados => Set<DocumentoProcesado>();
    public DbSet<DocumentoChunk> DocumentoChunks => Set<DocumentoChunk>();
    public DbSet<DocumentoIndexado> DocumentosIndexados => Set<DocumentoIndexado>();
    public DbSet<EmbeddingConfiguracion> ConfiguracionesEmbedding => Set<EmbeddingConfiguracion>();
    public DbSet<FuenteConocimiento> FuentesConocimiento => Set<FuenteConocimiento>();
    public DbSet<AsistenteFuente> AsistentesFuentes => Set<AsistenteFuente>();
    public DbSet<DocumentoFuente> DocumentosFuentes => Set<DocumentoFuente>();
    public DbSet<ConfiguracionRAG> ConfiguracionesRAG => Set<ConfiguracionRAG>();
    public DbSet<ConexionBaseDatos> ConexionesBaseDatos => Set<ConexionBaseDatos>();
    public DbSet<TablaAutorizada> TablasAutorizadas => Set<TablaAutorizada>();
    public DbSet<VistaAutorizada> VistasAutorizadas => Set<VistaAutorizada>();
    public DbSet<ConsultaEjecutada> ConsultasEjecutadas => Set<ConsultaEjecutada>();
    public DbSet<ConsultaPlantilla> ConsultasPlantillas => Set<ConsultaPlantilla>();
    public DbSet<ConfiguracionMotorConsultas> ConfiguracionesMotorConsultas => Set<ConfiguracionMotorConsultas>();
    public DbSet<Herramienta> Herramientas => Set<Herramienta>();
    public DbSet<AsistenteHerramienta> AsistentesHerramientas => Set<AsistenteHerramienta>();

    // Plataforma Multiagente (ETAPA 16)
    public DbSet<AgenteWorkflow> AgentesWorkflows => Set<AgenteWorkflow>();
    public DbSet<AgenteRol> AgentesRoles => Set<AgenteRol>();
    public DbSet<AgenteVersion> AgentesVersiones => Set<AgenteVersion>();
    public DbSet<EjecucionHerramienta> EjecucionesHerramientas => Set<EjecucionHerramienta>();
    public DbSet<ConfiguracionOrchestrator> ConfiguracionesOrchestrator => Set<ConfiguracionOrchestrator>();

    // Agent Orchestrator (ETAPA 17)
    public DbSet<AgentExecution> AgentExecutions => Set<AgentExecution>();
    public DbSet<AgentExecutionStep> AgentExecutionSteps => Set<AgentExecutionStep>();
    public DbSet<AgentCollaborationRule> AgentCollaborationRules => Set<AgentCollaborationRule>();
    public DbSet<AgentExecutionTrace> AgentExecutionTraces => Set<AgentExecutionTrace>();

    // Planner Engine (ETAPA 18)
    public DbSet<Plan> Planes => Set<Plan>();
    public DbSet<PlanStep> PlanSteps => Set<PlanStep>();
    public DbSet<PlanDependency> PlanDependencies => Set<PlanDependency>();
    public DbSet<PlanExecutionLog> PlanExecutionLogs => Set<PlanExecutionLog>();

    // Motor de Workflows (Workflow Engine) - ETAPA 12
    public DbSet<Workflow> Workflows => Set<Workflow>();
    public DbSet<WorkflowPaso> WorkflowPasos => Set<WorkflowPaso>();
    public DbSet<WorkflowEjecucion> WorkflowEjecuciones => Set<WorkflowEjecucion>();
    public DbSet<WorkflowPasoEjecucion> WorkflowPasosEjecucion => Set<WorkflowPasoEjecucion>();
    public DbSet<ConfiguracionWorkflow> ConfiguracionesWorkflow => Set<ConfiguracionWorkflow>();

    // Motor de Eventos Empresariales (Event Motor) - ETAPA 13
    public DbSet<EventoEmpresarial> EventosEmpresariales => Set<EventoEmpresarial>();
    public DbSet<ReglaEvento> ReglasEvento => Set<ReglaEvento>();
    public DbSet<EventoProcesado> EventosProcesados => Set<EventoProcesado>();
    public DbSet<TareaProgramada> TareasProgramadas => Set<TareaProgramada>();
    public DbSet<ConfiguracionEventoMotor> ConfiguracionEventoMotor => Set<ConfiguracionEventoMotor>();

    // Seguridad, Gobierno y Observabilidad (ETAPA 14)
    public DbSet<Permiso> Permisos => Set<Permiso>();
    public DbSet<RolPermiso> RolPermisos => Set<RolPermiso>();
    public DbSet<UsuarioAsistente> UsuarioAsistentes => Set<UsuarioAsistente>();
    public DbSet<UsuarioFuente> UsuarioFuentes => Set<UsuarioFuente>();
    public DbSet<PoliticaIA> PoliticasIA => Set<PoliticaIA>();
    public DbSet<AuditoriaIA> AuditoriaIA => Set<AuditoriaIA>();
    public DbSet<MetricasIA> MetricasIA => Set<MetricasIA>();
    public DbSet<AuditoriaActividad> AuditoriaActividad => Set<AuditoriaActividad>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AsistenteDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
