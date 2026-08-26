# Manual Técnico — ETAPA 11: Motor de Herramientas (Tool Orchestrator)

## 1. Requisitos / Stack
- ASP.NET Core 8 (API + Web MVC), C#, Bootstrap 5
- Microsoft SQL Server + Entity Framework Core 8
- Ollama + `DeepSeek-R1-Distill-Qwen-7B` (decisión y respuesta)
- ChromaDB (RAG, usado por `DocumentSearchTool`)

## 2. Modelo de datos (nuevas tablas)
| Tabla | Descripción |
|-------|-------------|
| `Herramientas` | Catálogo de herramientas (IdHerramienta, Nombre, Codigo [único], Descripcion, Categoria, Activa, RequierePermiso, FechaRegistro) |
| `AsistentesHerramientas` | Relación Asistente↔Herramienta (IdAsistente, IdHerramienta, Activa) |
| `EjecucionesHerramientas` | Auditoría de cada ejecución (IdEjecucion, IdHerramienta, IdUsuario, FechaHora, Parametros, Resultado, TiempoEjecucion, Estado) |
| `ConfiguracionOrchestrator` | Configuración global (Habilitado, Prioridad, TiempoMaximoEjecucionMs, MaxEjecucionesSimultaneas, RequiereAutorizacion) |

## 3. Migración y seed
- Migración: `Etapa11ToolOrchestrator` (aplicada con `dotnet ef database update`).
- `DbInitializer` siembra 5 herramientas:
  - `DocumentSearchTool` (ConsultaDocumental, RequierePermiso=true)
  - `SqlQueryTool` (ConsultaSQL, RequierePermiso=true)
  - `CalculatorTool` (Utilidad, RequierePermiso=false)
  - `DateTimeTool` (Utilidad, RequierePermiso=false)
  - `ReportTool` (Reporte, RequierePermiso=false)
- Y un registro en `ConfiguracionOrchestrator` (Habilitado=true, Prioridad=100,
  TiempoMaximoEjecucionMs=30000, MaxEjecucionesSimultaneas=4, RequiereAutorizacion=true).

## 4. Servicios clave
- `ToolOrchestrator` (`IToolOrchestrator`): registro, descubrimiento, resolución, ejecución,
  permisos y auditoría.
- `DecisionHerramientaService` (`IDecisionHerramientaService`): motor de decisión. Pide a
  DeepSeek un JSON `{ "herramienta": "<codigo>", "parametros": { ... } }` cuando la pregunta
  requiere una herramienta; si no, responde con texto normal.
- `HerramientaService`: CRUD de herramientas + estadísticas de ejecución.
- Herramientas `ITool`: `DocumentSearchTool`, `SqlQueryTool`, `CalculatorTool`, `DateTimeTool`,
  `ReportTool`.

## 5. Integración con el chat
En `ChatService.ProcesarMensajeAsync`:
- Si el asistente tiene herramientas asociadas (`usoOrquestador=true`), se omite el path
  automático de RAG/SQL y se delega al orquestador (decisión + ejecución + contexto).
- El `Contenido` de la herramienta se agrega al system prompt; el modelo genera la respuesta.
- `MensajeResponse.HerramientasUsadas` (tipo `HerramientaUsoChatDto`) alimenta los badges del chat.

## 6. API (endpoints, todos `[Authorize]`)
| Método | Ruta | Descripción |
|--------|------|-------------|
| GET | `/api/herramientas?idAsistente=` | Listar herramientas (+ stats) |
| GET | `/api/herramientas/{id}` | Detalle |
| POST | `/api/herramientas` | Crear |
| PUT | `/api/herramientas/{id}` | Actualizar |
| PUT | `/api/herramientas/{id}/activar` | Activar |
| PUT | `/api/herramientas/{id}/desactivar` | Desactivar |
| GET | `/api/asistenteherramientas/{idAsistente}/herramientas` | Herramientas del asistente |
| POST | `/api/asistenteherramientas/{idAsistente}/herramientas/{idHerramienta}?activa=` | Asociar |
| DELETE | `/api/asistenteherramientas/{idAsistente}/herramientas/{idHerramienta}` | Desasociar |
| GET | `/api/ejecucionesherramientas` | Monitoreo (auditoría) |
| GET/PUT | `/api/orchestratorconfig` | Configuración del orquestador |

## 7. Web (MVC, rol Administrador salvo Monitoreo que también admite Supervisor)
- `Herramientas` (registro, activar/desactivar)
- `AsistenteHerramientas` (asociar herramientas a asistentes)
- `MonitoreoHerramientas` (ejecuciones, tiempos, errores)
- `OrchestratorConfig` (políticas)
- `Chat` muestra badges de "Herramientas utilizadas" en cada respuesta del asistente.

## 8. Reglas de negocio / permisos
- Usuario autenticado, rol válido, herramienta activa y (si `RequierePermiso` y
  `RequiereAutorizacion`) asociada al asistente. Si falla → se rechaza y se audita como "Rechazada".
- `SqlQueryTool` solo ejecuta SELECT sobre tablas/vistas autorizadas; rechaza inserciones/updates/deletes.

## 9. Logging (Serilog)
El orquestador registra: `Ejecutando herramienta '{Codigo}' para usuario {Usuario}.` y errores
de ejecución. La auditoría es siempre persistente (los fallos de auditoría se registran como warning,
nunca abortan la ejecución).
