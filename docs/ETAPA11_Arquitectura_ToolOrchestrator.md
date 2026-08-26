# Arquitectura del Tool Orchestrator (ETAPA 11)

## 1. Visión general

El **Motor de Herramientas (Tool Orchestrator)** permite al Asistente Inteligente Empresarial
ejecutar acciones controladas sobre servicios internos (RAG/ChromaDB, SQL Server, cálculo,
fecha/hora, generación de reportes) sin acoplar la lógica de IA al acceso a datos. Cumple con
Clean Architecture, SOLID, inyección de dependencias, DTO, FluentValidation, Serilog y separación
estricta entre el modelo y las herramientas.

## 2. Capas

```
Asistente.Domain
  - Entities: Herramienta, AsistenteHerramienta, EjecucionHerramienta, ConfiguracionOrchestrator
  - Interfaces: ITool, IToolOrchestrator, IHerramientaRepository, IAsistenteHerramientaRepository,
                IEjecucionHerramientaRepository, IConfiguracionOrchestratorRepository, IUsuarioRepository
  - Enums: CategoriaHerramienta (valores sugeridos del RF: ConsultaDocumental, ConsultaSQL,
           Utilidad, Reporte, Integracion, Sistema)

Asistente.Application
  - Herramientas (ITool): DocumentSearchTool, SqlQueryTool, CalculatorTool, DateTimeTool, ReportTool
  - Services: ToolOrchestrator (ejecución + auditoría + permisos),
              DecisionHerramientaService (motor de decisión basado en LLM),
              HerramientaService (CRUD + estadísticas), EjecucionHerramientaService
  - Interfaces: IHerramientaService, IEjecucionHerramientaService, IDecisionHerramientaService

Asistente.Infrastructure
  - EF Configurations, Repositories, DbInitializer (seed de 5 herramientas + configuración)
  - Migración: 20260805214356_Etapa11ToolOrchestrator

Asistente.API        -> Controllers: Herramientas, AsistenteHerramientas,
                                     EjecucionesHerramientas, OrchestratorConfig
Asistente.Web        -> Controllers MVC + Views (Herramientas, AsistenteHerramientas,
                                     MonitoreoHerramientas, OrchestratorConfig) + chat.js (badges)
```

## 3. Contratos principales

```csharp
public interface ITool
{
    string Name { get; }
    string Description { get; }
    Task<ToolExecutionResult> ExecuteAsync(ToolExecutionRequest request, CancellationToken ct = default);
}

public class ToolExecutionRequest
{
    public string HerramientaCodigo { get; set; } = "";
    public Dictionary<string, object> Parametros { get; set; } = new();
    public int IdUsuario { get; set; }
    public int? IdAsistente { get; set; }
    public string PreguntaOriginal { get; set; } = "";
}

public class ToolExecutionResult
{
    public bool Exitoso { get; set; }
    public string? Error { get; set; }
    public string? Contenido { get; set; }   // resultado estructurado para el contexto
}
```

## 4. Flujo (Motor de decisión + Orquestador)

1. El usuario pregunta en el chat asociado a un asistente.
2. `ChatService` obtiene las herramientas autorizadas del asistente
   (`IToolOrchestrator.ObtenerHerramientasParaAsistenteAsync`).
3. Si hay herramientas, `DecisionHerramientaService.DecidirAsync` consulta a **DeepSeek** con un
   system prompt de *function calling* y devuelve `{ herramienta, parametros }` o `RequiereHerramienta=false`.
4. Si se requiere herramienta, `ToolOrchestrator.EjecutarAsync`:
   - Resuelve la herramienta por código.
   - Valida estado (activa) y permisos (usuario autenticado, rol, asociación al asistente,
     política `RequiereAutorizacion`).
   - Ejecuta la implementación `ITool` con timeout configurable.
   - Registra `EjecucionHerramienta` (auditoría) en cualquier caso (éxito/error/rechazo).
5. El `Contenido` de la herramienta se inyecta al system prompt y el modelo genera la respuesta final.
6. `MensajeResponse.HerramientasUsadas` se devuelve para mostrar badges en el chat.

## 5. Seguridad y trazabilidad

- El modelo **nunca** accede directamente a SQL Server ni a otros servicios; siempre pasa por el orquestador.
- Toda ejecución queda registrada en `EjecucionesHerramientas` (usuario, herramienta, parámetros,
  resultado, tiempo, estado).
- `SqlQueryTool` ejecuta **solo SELECT** sobre tablas/vistas autorizadas del motor de consultas
  existente (reutiliza `IConexionBaseDatosRepository`, `IConexionCifrador`, `ISqlQueryExecutor`).
- Desactivar una herramienta la excluye inmediatamente del set ofrecido al modelo.

## 6. Extensibilidad

Para agregar una herramienta:
1. Crear `class MiTool : ITool` en `Asistente.Application/Services/Herramientas/`.
2. Registrarla con `services.AddScoped<ITool, MiTool>();` en `Application/DependencyInjection.cs`.
3. Registrarla en BD vía pantalla "Herramientas" (o seed). El núcleo no requiere cambios.

## 7. Configuración (Actividad 10)

`ConfiguracionOrchestrator` administra: Habilitado, Prioridad, TiempoMaximoEjecucionMs,
MaxEjecucionesSimultaneas, RequiereAutorizacion. Se administra desde la UI
(Configuración → Config. Orquestador).
