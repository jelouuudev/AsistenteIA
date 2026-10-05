using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Asistente.Shared;

namespace Asistente.Web.Services;

public interface IApiService
{
    Task<MensajeResponse> EnviarMensajeAsync(MensajeRequest request);
    Task<LoginResponse> LoginAsync(LoginRequest request);
    Task LogoutAsync(int sessionId);

    // Usuarios
    Task<IEnumerable<UsuarioDto>> GetUsuariosAsync(int currentUserId, string ip);
    Task<UsuarioDto?> GetUsuarioByIdAsync(int id, int currentUserId, string ip);
    Task<UsuarioDto> CrearUsuarioAsync(CrearUsuarioRequest request, int currentUserId, string ip);
    Task<UsuarioDto> ActualizarUsuarioAsync(int id, ActualizarUsuarioRequest request, int currentUserId, string ip);
    Task DesactivarUsuarioAsync(int id, int currentUserId, string ip);
    Task CambiarPasswordAsync(int id, CambiarPasswordRequest request, int currentUserId, string ip);

    // Roles
    Task<IEnumerable<RolDto>> GetRolesAsync(int currentUserId, string ip);
    Task<RolDto?> GetRolByIdAsync(int id, int currentUserId, string ip);
    Task<RolDto> CrearRolAsync(CrearRolRequest request, int currentUserId, string ip);
    Task<RolDto> ActualizarRolAsync(int id, ActualizarRolRequest request, int currentUserId, string ip);

    // Auditoria
    Task<IEnumerable<AuditoriaSesionDto>> GetAuditoriaSesionesAsync(int currentUserId, string ip);
    Task<IEnumerable<AuditoriaActividadDto>> GetAuditoriaActividadesAsync(int currentUserId, string ip);

    // Asistentes (ETAPA 16 - Plataforma Multiagente)
    Task<IEnumerable<AsistenteDto>> GetAsistentesAsync(int currentUserId, string ip);
    Task<IEnumerable<AsistenteDto>> GetAgentesAutorizadosAsync(int idUsuario, int currentUserId, string ip);
    Task<AsistenteDto?> GetAsistenteByIdAsync(int id, int currentUserId, string ip);
    Task<AsistenteDto> CrearAsistenteAsync(CrearAsistenteRequest request, int currentUserId, string ip);
    Task<AsistenteDto> ActualizarAsistenteAsync(int id, ActualizarAsistenteRequest request, int currentUserId, string ip);
    Task ActivarAsistenteAsync(int id, int currentUserId, string ip);
    Task DesactivarAsistenteAsync(int id, int currentUserId, string ip);
    Task PublicarAgenteAsync(int id, int currentUserId, string ip);
    Task EnviarAgenteAPruebaAsync(int id, int currentUserId, string ip);
    Task<AsistenteDto> DuplicarAgenteAsync(int id, int currentUserId, string ip);
    Task<AgenteVersionDto> CrearVersionAgenteAsync(int id, int currentUserId, string ip);
    Task<AgenteVersionDto> RestaurarVersionAgenteAsync(int id, int idVersion, int currentUserId, string ip);
    Task<IEnumerable<AgenteVersionDto>> GetVersionesAgenteAsync(int id, int currentUserId, string ip);
    Task<IEnumerable<PromptSistemaDto>> GetPromptsByAsistenteIdAsync(int asistenteId, int currentUserId, string ip);
    Task<PromptSistemaDto?> GetPromptActivoByAsistenteIdAsync(int asistenteId, int currentUserId, string ip);

    // Prompts
    Task<IEnumerable<PromptSistemaDto>> GetPromptsAsync(int currentUserId, string ip);
    Task<PromptSistemaDto?> GetPromptByIdAsync(int id, int currentUserId, string ip);
    Task<PromptSistemaDto> CrearPromptAsync(CrearPromptRequest request, int currentUserId, string ip);
    Task<PromptSistemaDto> ActualizarPromptAsync(int id, ActualizarPromptRequest request, int currentUserId, string ip);
    Task ActivarPromptAsync(int id, int currentUserId, string ip);
    Task DesactivarPromptAsync(int id, int currentUserId, string ip);
    Task EliminarPromptAsync(int id, int currentUserId, string ip);
    Task<PromptSistemaDto> DuplicarPromptAsync(int id, int currentUserId, string ip);
    Task<PromptSistemaDto> RestaurarPromptDesdeHistorialAsync(int id, int idHistorial, int currentUserId, string ip);
    Task<IEnumerable<HistorialPromptDto>> GetHistorialPromptAsync(int promptId, int currentUserId, string ip);

    // Fuentes de Conocimiento
    Task<IEnumerable<FuenteConocimientoDto>> GetFuentesConocimientoAsync(int currentUserId, string ip);
    Task<IEnumerable<FuenteConocimientoDto>> GetFuentesDocumentoAsync(int idDocumento, int currentUserId, string ip);
    Task AsignarFuentesDocumentoAsync(int idDocumento, List<int> fuentes, int currentUserId, string ip);

    // Prueba Asistente
    Task<PruebaAsistenteResponse> ProbarAsistenteAsync(PruebaAsistenteRequest request, int currentUserId, string ip);

    // Memoria - Conversaciones
    Task<IEnumerable<ConversacionListDto>> GetConversacionesAsync(int currentUserId, string ip);
    Task<IEnumerable<ConversacionListDto>> BuscarConversacionesAsync(string query, int currentUserId, string ip);
    Task<ConversacionDto?> GetConversacionByIdAsync(int id, int currentUserId, string ip);
    Task<ConversacionDto> CrearConversacionAsync(int currentUserId, string ip);
    Task RenombrarConversacionAsync(int id, string titulo, int currentUserId, string ip);
    Task ArchivarConversacionAsync(int id, int currentUserId, string ip);
    Task EliminarConversacionAsync(int id, int currentUserId, string ip);
    Task<DebugContextoDto?> GetDebugContextoAsync(int idConversacion, int currentUserId, string ip);

    // Configuracion Memoria
    Task<ConfiguracionMemoriaDto> GetConfiguracionMemoriaAsync(int currentUserId, string ip);
    Task<ConfiguracionMemoriaDto> ActualizarConfiguracionMemoriaAsync(ActualizarConfiguracionMemoriaRequest request, int currentUserId, string ip);

    // Categorias Documento
    Task<IEnumerable<CategoriaDocumentoDto>> GetCategoriasDocumentoAsync(int currentUserId, string ip);
    Task<IEnumerable<CategoriaDocumentoDto>> GetCategoriasDocumentoActivasAsync(int currentUserId, string ip);
    Task<CategoriaDocumentoDto?> GetCategoriaDocumentoByIdAsync(int id, int currentUserId, string ip);
    Task<CategoriaDocumentoDto> CrearCategoriaDocumentoAsync(CrearCategoriaDocumentoRequest request, int currentUserId, string ip);
    Task<CategoriaDocumentoDto> ActualizarCategoriaDocumentoAsync(int id, ActualizarCategoriaDocumentoRequest request, int currentUserId, string ip);

    // Documentos
    Task<IEnumerable<DocumentoDto>> GetDocumentosFiltradosAsync(FiltroDocumentoRequest request, int currentUserId, string ip);
    Task<DocumentoDto?> GetDocumentoByIdAsync(int id, int currentUserId, string ip);
    Task<DocumentoDto> CrearDocumentoAsync(CrearDocumentoRequest request, int currentUserId, string ip);
    Task<DocumentoDto> ActualizarDocumentoAsync(int id, ActualizarDocumentoRequest request, int currentUserId, string ip);
    Task<IEnumerable<DocumentoDto>> GetDocumentosDisponiblesAsync(int? idFuenteExcluir, int currentUserId, string ip);
    Task ActivarDocumentoAsync(int id, int currentUserId, string ip);
    Task ArchivarDocumentoAsync(int id, int currentUserId, string ip);
    Task EliminarDocumentoAsync(int id, int currentUserId, string ip);

    // Versiones
    Task<IEnumerable<DocumentoVersionDto>> GetDocumentoVersionesAsync(int documentoId, int currentUserId, string ip);
    Task<DocumentoVersionDto> CargarDocumentoVersionAsync(int documentoId, string nombreArchivo, Stream archivoStream, int currentUserId, string ip);
    Task<(Stream fileStream, string fileName, string contentType)> DescargarDocumentoVersionAsync(int documentoId, int versionId, int currentUserId, string ip);

    // Auditoría
    Task<IEnumerable<AuditoriaDocumentalDto>> GetAuditoriaDocumentoAsync(int documentoId, int currentUserId, string ip);
    Task<IEnumerable<AuditoriaDocumentalDto>> GetTodasAuditoriasDocumentoAsync(int currentUserId, string ip);

    // Procesamiento Documental
    Task<IEnumerable<DocumentoProcesadoDto>> GetProcesamientoAllAsync(int currentUserId, string ip);
    Task<DashboardProcesamientoDto> GetProcesamientoDashboardAsync(int currentUserId, string ip);
    Task<DocumentoProcesadoDto?> GetProcesamientoByIdAsync(int id, int currentUserId, string ip);
    Task<IEnumerable<DocumentoProcesadoDto>> GetProcesamientoByEstadoAsync(string estado, int currentUserId, string ip);
    Task<DocumentoProcesadoDto?> GetProcesamientoByVersionIdAsync(int versionId, int currentUserId, string ip);
    Task<IEnumerable<DocumentoChunkDto>> GetChunksAsync(int procesadoId, int currentUserId, string ip);
    Task ProcesarDocumentoAsync(int versionId, int currentUserId, string ip);
    Task ReprocesarDocumentoAsync(int procesadoId, int currentUserId, string ip);

    // Indexacion
    Task<IEnumerable<DocumentoIndexadoDto>> GetIndexacionAllAsync(int currentUserId, string ip);
    Task<DashboardIndexacionDto> GetIndexacionDashboardAsync(int currentUserId, string ip);
    Task<DocumentoIndexadoDto?> GetIndexacionByIdAsync(int id, int currentUserId, string ip);
    Task<DocumentoIndexadoDto?> GetIndexacionByProcesadoIdAsync(int procesadoId, int currentUserId, string ip);
    Task<IEnumerable<DocumentoIndexadoDto>> GetIndexacionByEstadoAsync(string estado, int currentUserId, string ip);
    Task IndexarDocumentoAsync(int documentoProcesadoId, int currentUserId, string ip);
    Task ReindexarDocumentoAsync(int documentoProcesadoId, int currentUserId, string ip);
    Task ReindexarTodosAsync(int currentUserId, string ip);
    Task EliminarIndiceAsync(int documentoProcesadoId, int currentUserId, string ip);

    // Embedding Configuracion
    Task<IEnumerable<EmbeddingConfiguracionDto>> GetEmbeddingConfiguracionesAsync(int currentUserId, string ip);
    Task<EmbeddingConfiguracionDto?> GetEmbeddingConfiguracionActivaAsync(int currentUserId, string ip);
    Task<EmbeddingConfiguracionDto> ActualizarEmbeddingConfiguracionAsync(int id, ActualizarEmbeddingConfiguracionRequest request, int currentUserId, string ip);
    Task<EmbeddingConfiguracionDto> CrearEmbeddingConfiguracionAsync(ActualizarEmbeddingConfiguracionRequest request, int currentUserId, string ip);

    // Busqueda Semantica
    Task<BusquedaSemanticaResponse> BuscarSemanticamenteAsync(BusquedaSemanticaRequest request, int currentUserId, string ip);
    Task<IEnumerable<FuenteConocimientoDto>> GetFuentesConocimientoActivasAsync(int currentUserId, string ip);
    Task<FuenteConocimientoDto?> GetFuenteConocimientoByIdAsync(int id, int currentUserId, string ip);
    Task<FuenteConocimientoDto> CrearFuenteConocimientoAsync(CrearFuenteConocimientoRequest request, int currentUserId, string ip);
    Task<FuenteConocimientoDto> ActualizarFuenteConocimientoAsync(int id, ActualizarFuenteConocimientoRequest request, int currentUserId, string ip);
    Task ActivarFuenteConocimientoAsync(int id, int currentUserId, string ip);
    Task DesactivarFuenteConocimientoAsync(int id, int currentUserId, string ip);
    Task<DashboardFuentesDto> GetDashboardFuentesAsync(int currentUserId, string ip);
    Task<IEnumerable<AsistenteFuenteDto>> GetFuentesDeAsistenteAsync(int idAsistente, int currentUserId, string ip);
    Task<IEnumerable<AsistenteFuenteDto>> GetAsistentesDeFuenteAsync(int idFuente, int currentUserId, string ip);
    Task AsignarFuenteAAsistenteAsync(AsignarFuenteAAsistenteRequest request, int currentUserId, string ip);
    Task DesasignarFuenteDeAsistenteAsync(int idAsistente, int idFuente, int currentUserId, string ip);
    Task<IEnumerable<DocumentoFuenteDto>> GetDocumentosDeFuenteAsync(int idFuente, int currentUserId, string ip);
    Task AsignarDocumentoAFuenteAsync(AsignarDocumentoAFuenteRequest request, int currentUserId, string ip);
    Task DesasignarDocumentoDeFuenteAsync(int idDocumento, int idFuente, int currentUserId, string ip);

    Task<ConfiguracionRAGDto> GetConfiguracionRAGAsync(int currentUserId, string ip);
    Task<ConfiguracionRAGDto> ActualizarConfiguracionRAGAsync(ActualizarConfiguracionRAGRequest request, int currentUserId, string ip);

    // Motor de Consultas Empresariales
    Task<IEnumerable<ConexionBaseDatosDto>> GetConexionesBaseDatosAsync(int currentUserId, string ip);
    Task<IEnumerable<ConexionBaseDatosDto>> GetConexionesBaseDatosActivasAsync(int currentUserId, string ip);
    Task<ConexionBaseDatosDto?> GetConexionBaseDatosByIdAsync(int id, int currentUserId, string ip);
    Task<ConexionBaseDatosDto> CrearConexionBaseDatosAsync(CrearConexionBaseDatosRequest request, int currentUserId, string ip);
    Task<ConexionBaseDatosDto> ActualizarConexionBaseDatosAsync(int id, ActualizarConexionBaseDatosRequest request, int currentUserId, string ip);
    Task EliminarConexionBaseDatosAsync(int id, int currentUserId, string ip);
    Task ActivarConexionBaseDatosAsync(int id, int currentUserId, string ip);
    Task DesactivarConexionBaseDatosAsync(int id, int currentUserId, string ip);
    Task<ConexionPruebaResultadoDto> ProbarConexionBaseDatosAsync(ProbarConexionRequest request, int currentUserId, string ip);
    Task<EsquemaBaseDatosDto> DescubrirEsquemaAsync(int idConexion, int currentUserId, string ip);
    Task<IEnumerable<TablaAutorizadaDto>> GetTablasAutorizadasAsync(int idConexion, int currentUserId, string ip);
    Task<IEnumerable<VistaAutorizadaDto>> GetVistasAutorizadasAsync(int idConexion, int currentUserId, string ip);
    Task AgregarTablaAutorizadaAsync(int idConexion, TablaAutorizadaRequest request, int currentUserId, string ip);
    Task AgregarVistaAutorizadaAsync(int idConexion, VistaAutorizadaRequest request, int currentUserId, string ip);
    Task EliminarTablaAutorizadaAsync(int idConexion, int idTabla, int currentUserId, string ip);
    Task EliminarVistaAutorizadaAsync(int idConexion, int idVista, int currentUserId, string ip);

    // Consultas Ejecutadas
    Task<IEnumerable<ConsultaEjecutadaDto>> GetConsultasEjecutadasAsync(int currentUserId, string ip);
    Task<IEnumerable<ConsultaEjecutadaDto>> GetConsultasEjecutadasPorUsuarioAsync(int idUsuario, int currentUserId, string ip);
    Task<DashboardConsultasDto> GetDashboardConsultasAsync(int currentUserId, string ip);
    Task<ResultadoProcesarPreguntaDto> ProcesarPreguntaEmpresarialAsync(ProcesarPreguntaRequest request, int currentUserId, string ip);
    Task<EjecutarConsultaResponse> EjecutarConsultaEmpresarialAsync(EjecutarConsultaRequest request, int currentUserId, string ip);

    // Plantillas
    Task<IEnumerable<ConsultaPlantillaDto>> GetConsultasPlantillasAsync(int currentUserId, string ip);
    Task<ConsultaPlantillaDto?> GetConsultaPlantillaByIdAsync(int id, int currentUserId, string ip);
    Task<ConsultaPlantillaDto> CrearConsultaPlantillaAsync(CrearConsultaPlantillaRequest request, int currentUserId, string ip);
    Task<ConsultaPlantillaDto> ActualizarConsultaPlantillaAsync(int id, ActualizarConsultaPlantillaRequest request, int currentUserId, string ip);
    Task EliminarConsultaPlantillaAsync(int id, int currentUserId, string ip);
    Task ActivarConsultaPlantillaAsync(int id, int currentUserId, string ip);
    Task DesactivarConsultaPlantillaAsync(int id, int currentUserId, string ip);

    // Configuracion Motor
    Task<ConfiguracionMotorConsultasDto?> GetConfiguracionMotorConsultasAsync(int currentUserId, string ip);
    Task<ConfiguracionMotorConsultasDto> ActualizarConfiguracionMotorConsultasAsync(ActualizarConfiguracionMotorConsultasRequest request, int currentUserId, string ip);

    // Motor de Herramientas (Tool Orchestrator) - ETAPA 11
    Task<List<HerramientaDto>> GetHerramientasAsync(int? idAsistente, int currentUserId, string ip);
    Task<HerramientaDto> CrearHerramientaAsync(CrearHerramientaRequest request, int currentUserId, string ip);
    Task ActualizarHerramientaAsync(int id, ActualizarHerramientaRequest request, int currentUserId, string ip);
    Task ActivarHerramientaAsync(int id, int currentUserId, string ip);
    Task DesactivarHerramientaAsync(int id, int currentUserId, string ip);
    Task<List<HerramientaDto>> GetHerramientasDeAsistenteAsync(int idAsistente, int currentUserId, string ip);
    Task AsociarHerramientaAsync(int idAsistente, int idHerramienta, bool activa, int currentUserId, string ip);
    Task DesasociarHerramientaAsync(int idAsistente, int idHerramienta, int currentUserId, string ip);
    Task<List<EjecucionHerramientaDto>> GetEjecucionesHerramientasAsync(int currentUserId, string ip);
    Task<ConfiguracionOrchestratorDto> GetConfiguracionOrchestratorAsync(int currentUserId, string ip);
    Task GuardarConfiguracionOrchestratorAsync(ConfiguracionOrchestratorDto config, int currentUserId, string ip);

    // Agent Orchestrator (ETAPA 17)
    Task<AgentExecutionResultDto?> ExecuteOrchestratorAsync(int idAgentePrincipal, string pregunta, int currentUserId, string ip);
    Task<object?> GetOrchestratorDashboardAsync(int currentUserId, string ip);
    Task<object?> GetOrchestratorTrazasAsync(int idExecution, int currentUserId, string ip);
    Task<List<AgentCollaborationRuleDto>> GetReglasColaboracionAsync(int currentUserId, string ip);
    Task<AgentCollaborationRuleDto> CrearReglaColaboracionAsync(AgentCollaborationRuleDto regla, int currentUserId, string ip);
    Task ActualizarReglaColaboracionAsync(int id, AgentCollaborationRuleDto regla, int currentUserId, string ip);
    Task EliminarReglaColaboracionAsync(int id, int currentUserId, string ip);
    Task<List<AgenteSimpleDto>> GetAgentesParaOrquestadorAsync(int currentUserId, string ip);

    // Motor de Workflows (Workflow Engine) - ETAPA 12
    Task<List<WorkflowDto>> GetWorkflowsAsync(int currentUserId, string ip);
    Task<WorkflowDto?> GetWorkflowByIdAsync(int id, int currentUserId, string ip);
    Task<WorkflowDto> CrearWorkflowAsync(CrearWorkflowRequest request, int currentUserId, string ip);
    Task ActualizarWorkflowAsync(int id, ActualizarWorkflowRequest request, int currentUserId, string ip);
    Task ActivarWorkflowAsync(int id, int currentUserId, string ip);
    Task DesactivarWorkflowAsync(int id, int currentUserId, string ip);
    Task VersionarWorkflowAsync(int id, int currentUserId, string ip);
    Task EliminarWorkflowAsync(int id, int currentUserId, string ip);
    Task<List<WorkflowEjecucionDto>> GetWorkflowEjecucionesAsync(int currentUserId, string ip);
    Task<ConfiguracionWorkflowDto> GetConfiguracionWorkflowAsync(int currentUserId, string ip);
    Task GuardarConfiguracionWorkflowAsync(ConfiguracionWorkflowDto config, int currentUserId, string ip);

    // Motor de Eventos Empresariales (Event Motor) - ETAPA 13
    Task<List<EventoEmpresarialDto>> GetEventosEmpresarialesAsync(int currentUserId, string ip);
    Task<EventoEmpresarialDto?> GetEventoEmpresarialByIdAsync(int id, int currentUserId, string ip);
    Task<EventoEmpresarialDto> CrearEventoEmpresarialAsync(CrearEventoEmpresarialRequest request, int currentUserId, string ip);
    Task ActualizarEventoEmpresarialAsync(int id, ActualizarEventoEmpresarialRequest request, int currentUserId, string ip);
    Task ActivarEventoEmpresarialAsync(int id, int currentUserId, string ip);
    Task DesactivarEventoEmpresarialAsync(int id, int currentUserId, string ip);
    Task EliminarEventoEmpresarialAsync(int id, int currentUserId, string ip);
    Task<List<ReglaEventoDto>> GetReglasEventoAsync(int currentUserId, string ip);
    Task<ReglaEventoDto> CrearReglaEventoAsync(CrearReglaEventoRequest request, int currentUserId, string ip);
    Task ActualizarReglaEventoAsync(int id, ActualizarReglaEventoRequest request, int currentUserId, string ip);
    Task ActivarReglaEventoAsync(int id, int currentUserId, string ip);
    Task DesactivarReglaEventoAsync(int id, int currentUserId, string ip);
    Task EliminarReglaEventoAsync(int id, int currentUserId, string ip);
    Task<List<TareaProgramadaDto>> GetTareasProgramadasAsync(int currentUserId, string ip);
    Task<TareaProgramadaDto> CrearTareaProgramadaAsync(CrearTareaProgramadaRequest request, int currentUserId, string ip);
    Task ActualizarTareaProgramadaAsync(int id, ActualizarTareaProgramadaRequest request, int currentUserId, string ip);
    Task ActivarTareaProgramadaAsync(int id, int currentUserId, string ip);
    Task DesactivarTareaProgramadaAsync(int id, int currentUserId, string ip);
    Task EliminarTareaProgramadaAsync(int id, int currentUserId, string ip);
    Task<List<EventoProcesadoDto>> GetEventosProcesadosAsync(int currentUserId, string ip);
    Task<ConfiguracionEventoMotorDto> GetConfiguracionEventoMotorAsync(int currentUserId, string ip);
    Task GuardarConfiguracionEventoMotorAsync(ConfiguracionEventoMotorDto config, int currentUserId, string ip);
    Task<MonitoreoEventosDto> GetMonitoreoEventosAsync(int currentUserId, string ip);
    Task<EventoProcesadoDto> DispararEventoAsync(DispararEventoRequest request, int currentUserId, string ip);

    // Disparadores de Evento (ETAPA 13)
    Task<List<DisparadorEventoDto>> GetDisparadoresEventoAsync(int currentUserId, string ip);
    Task<DisparadorEventoDto?> GetDisparadorEventoByIdAsync(int id, int currentUserId, string ip);
    Task<DisparadorEventoDto> CrearDisparadorEventoAsync(CrearDisparadorEventoRequest request, int currentUserId, string ip);
    Task ActualizarDisparadorEventoAsync(int id, ActualizarDisparadorEventoRequest request, int currentUserId, string ip);
    Task ActivarDisparadorEventoAsync(int id, int currentUserId, string ip);
    Task DesactivarDisparadorEventoAsync(int id, int currentUserId, string ip);
    Task EliminarDisparadorEventoAsync(int id, int currentUserId, string ip);

    // Seguridad, Gobierno, Auditoría y Observabilidad (ETAPA 14)
    Task<IEnumerable<PermisoDto>> GetPermisosAsync(int currentUserId, string ip);
    Task<IEnumerable<PoliticaIADto>> GetPoliticasAsync(int currentUserId, string ip);
    Task<DashboardSeguridadDto> GetDashboardSeguridadAsync(int currentUserId, string ip);
    Task<List<AsistenteAutorizadoDto>> GetAsistentesDeUsuarioAsync(int idUsuario, int currentUserId, string ip);
    Task<List<FuenteAutorizadaDto>> GetFuentesDeUsuarioAsync(int idUsuario, int currentUserId, string ip);
    Task AsignarAsistentesUsuarioAsync(int idUsuario, AsignarAsistentesUsuarioRequest request, int currentUserId, string ip);
    Task AsignarFuentesUsuarioAsync(int idUsuario, AsignarFuentesUsuarioRequest request, int currentUserId, string ip);
    Task<List<PermisoAsignadoDto>> GetPermisosDeRolAsync(int idRol, int currentUserId, string ip);
    Task AsignarPermisosRolAsync(int idRol, AsignarPermisosRolRequest request, int currentUserId, string ip);

    // Planner Engine (ETAPA 18)
    Task<PlanDto> GenerarPlanAsync(string objetivo, int currentUserId, string ip);
    Task<ResultadoValidacionPlanDto> ValidarPlanAsync(int id, int currentUserId, string ip);
    Task<SimulacionPlanDto> SimularPlanAsync(int id, int currentUserId, string ip);
    Task<PlanDto> EjecutarPlanAsync(int id, int currentUserId, string ip);
    Task AprobarPlanAsync(int id, int currentUserId, string ip);
    Task CancelarPlanAsync(int id, int currentUserId, string ip);
    Task<PlannerDashboardDto> GetPlannerDashboardAsync(int currentUserId, string ip);
    Task<PlanDto> GetPlanAsync(int id, int currentUserId, string ip);

    // Centro de Aprobaciones / Human-in-the-Loop (ETAPA 19)
    Task<JsonElement> GetAprobacionesDashboardAsync(int currentUserId, string ip);
    Task<JsonElement> GetBandejaAprobacionesAsync(int currentUserId, string ip);
    Task<JsonElement> DecidirAprobacionAsync(int id, string decision, string? comentario, int currentUserId, string ip);
    Task<JsonElement> DelegarAprobacionAsync(int id, int idUsuarioDestino, string? comentario, int currentUserId, string ip);
}
