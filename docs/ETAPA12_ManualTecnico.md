# Manual Técnico — ETAPA 12: Motor de Automatización de Procesos Empresariales (Workflow Engine)

## 1. Objetivo

Implementar un **Motor de Flujos de Trabajo (Workflow Engine)** que ejecuta procesos
empresariales compuestos por múltiples pasos, utilizando las herramientas del *Tool
Orchestrator* (ETAPA 11) de forma secuencial, controlada, segura y auditable.

El motor es **totalmente desacoplado de las herramientas**: cada paso delega la ejecución
a `IToolOrchestrator`, reutilizando la autorización por asistente, la validación de
permisos y la auditoría de herramientas ya existentes.

## 2. Stack

- **ASP.NET Core 8** (C#), **Clean Architecture**, **SOLID**, **async/await**.
- **Entity Framework Core 8** + **SQL Server** (Windows Auth).
- **Ollama** + DeepSeek-R1-Distill-Qwen-7B (solo para la respuesta final del asistente).
- **FluentValidation**, **Serilog**, **DTO**, **Inyección de dependencias**.

## 3. Modelo de Datos (nuevas entidades)

| Entidad | Responsabilidad |
|---|---|
| `Workflow` | Definición del flujo: Nombre, Código, Descripción, Versión, Estado, Disparadores, FechaCreacion, UsuarioCreacion. |
| `WorkflowPaso` | Paso del flujo: Orden, Nombre, Herramienta (código), Parámetros (JSON), RequiereConfirmacion, ReintentosMaximos, TiempoMaximoMs, EstrategiaError. |
| `WorkflowEjecucion` | Auditoría de una ejecución completa: IdUsuario, IdAsistente, FechaInicio/Fin, Estado, TiempoTotal, ResultadoFinal. |
| `WorkflowPasoEjecucion` | Auditoría de cada paso: IdPaso, FechaInicio/Fin, Resultado, Estado, Observaciones. |
| `ConfiguracionWorkflow` | Configuración global (singleton): ReintentosMaximos, TiempoMaximoPasoMs, TiempoMaximoFlujoMs, ConfirmacionesObligatorias, LimitePasosPorWorkflow. |

`EstadoWorkflow`: `Borrador`, `Activo`, `Suspendido`, `Finalizado`, `Error`.
`EstrategiaError` (Actividad 5): `Reintentar`, `Omitir`, `Cancelar`, `RegistrarIncidencia`.

## 4. Componentes (capa Application)

### 4.1 `IWorkflowEngine` / `WorkflowEngine`
Núcleo del motor. Responsabilidades (Actividad 1):
1. Cargar la definición del flujo (`IWorkflowRepository.GetByIdAsync` + `Pasos`).
2. Ejecutar cada paso en orden (`foreach` sobre `Pasos.OrderBy(Orden)`).
3. Administrar **contexto compartido** (`Dictionary<string,string>`) indexado por
   `Paso{N}`, por código de `Herramienta` y por `resultado` (resultado del paso anterior).
4. Gestionar errores y **reintentos** (`ReintentosMaximos = max(paso, config)`).
5. Solicitar **confirmación** del usuario cuando `RequiereConfirmacion && !confirmado && ConfirmacionesObligatorias`.
6. Registrar auditoría de la ejecución y de cada paso (`WorkflowEjecucion`, `WorkflowPasoEjecucion`).

Métodos:
- `Task<WorkflowExecutionResult> EjecutarAsync(int idWorkflow, int idUsuario, int? idAsistente, bool confirmado, int? idEjecucionExistente, CancellationToken)`
- `Task<WorkflowExecutionResult?> ReanudarPendienteConfirmacionAsync(int idUsuario, int? idAsistente, CancellationToken)` — reanuda la ejecución en espera de confirmación (reutiliza `idEjecucionExistente` y salta los pasos ya Exitosa).

### 4.2 `SustituirContexto`
Sustituye tokens en los parámetros JSON del paso usando el contexto compartido:
`{{resultado}}`, `{{PasoN}}`, `{{Herramienta}}`. Soporta valores `string` y `JsonElement`.

### 4.3 `IWorkflowService` / `WorkflowService`
CRUD de workflows, activar/desactivar (cambio de `Estado`), versionar (clona el flujo y sus pasos en un nuevo registro con `Version + 1`),
eliminar y consulta de ejecuciones (`ObtenerEjecucionesAsync`).

### 4.4 `IWorkflowDecisionService` / `WorkflowDecisionService`
Identifica si un mensaje del usuario corresponde a un flujo activo, haciendo
*coincidencia determinista* de palabras clave contra `Workflow.Disparadores` (frases
separadas por `;`). Diseño determinista para el entorno de demostración (no depende del LLM).

### 4.5 Integración con `ChatService`
Tras el bloque del Tool Orchestrator (ETAPA 11), `ChatService`:
1. Si el mensaje es una confirmación (`EsMensajeConfirmacion`) → `ReanudarPendienteConfirmacionAsync`.
2. Si no, `WorkflowDecisionService.DecidirAsync` → si hay match → `WorkflowEngine.EjecutarAsync`.
3. El `ResultadoFinal` del flujo se inyecta en el *system prompt* (`contextoHerramientas`)
   para que DeepSeek lo presente; si requiere confirmación, se devuelve
   `RequiereConfirmacionWorkflow = true` y `MensajeConfirmacionWorkflow`.

## 5. API (capa API)

| Controlador | Rutas |
|---|---|
| `WorkflowsController` | `GET/POST /api/workflows`, `PUT /api/workflows/{id}`, `activar`, `desactivar`, `versionar`, `DELETE`. |
| `WorkflowEjecucionesController` | `GET /api/workflowejecuciones` (historial). |
| `ConfiguracionWorkflowController` | `GET/PUT /api/configuracionworkflow`. |

## 6. Web (MVC + Bootstrap 5)

- `WorkflowsController` (admin): listar, crear, editar, activar, desactivar, versionar, eliminar, configuración.
- `MonitoreoWorkflowsController`: panel de monitoreo de ejecuciones.
- Vistas: `Workflows/Index`, `Workflows/Crear`, `Workflows/Editar`, `Workflows/Configuracion`, `MonitoreoWorkflows/Index`.
- Menú `_Layout.cshtml`: ítem **Flujos** (admin) + **Monitoreo Flujos** (admin/supervisor) + ítem en Configuración.

## 7. Migraciones y Seed

- Migración: `Etapa12WorkflowEngine` (crea las 5 tablas + índices).
- `DbInitializer` siembra:
  - `ConfiguracionWorkflow` por defecto (singleton).
  - Workflow de ejemplo **"Reporte de Clientes"** (`REP-CLI`, Activo) con 3 pasos:
    1. *Consultar clientes* → `SqlQueryTool` `{"consulta":"SELECT COUNT(*) AS Total FROM Clientes;"}`
    2. *Generar reporte* → `ReportTool` `{"titulo":"Reporte de Clientes","contenido":"{{resultado}}"}`, `RequiereConfirmacion = true`
    3. *Presentar resultado* → `DateTimeTool` `{}`

## 8. Verificación

- **Build**: los 6 proyectos compilan sin errores.
- **Pruebas unitarias** (`WorkflowEngineTests`, xUnit + Moq): 5/5 verdes.
  Casos cubiertos: Caso 1 (3 pasos en orden), Caso 2 (reintentos antes de error),
  Caso 4 (auditoría por paso), contexto compartido (`{{resultado}}`), y confirmación
  (no ejecuta paso sensible sin confirmar).
- **Prueba de integración en runtime** (API + Ollama): enviar *"generar reporte de clientes"*
  ejecuta paso 1, solicita confirmación (`RequiereConfirmacionWorkflow = true`); al
  responder *"confirmar"* reanuda y completa los 3 pasos con estado **Exitosa** y
  presenta el resultado. Flujo verificado end-to-end.

## 9. Notas de diseño (criterios de aceptación)

- **Reutilizable entre asistentes**: los workflows son globales; su disponibilidad está
  dada por `Estado == Activo`, por lo que cualquier asistente puede dispararlo desde el chat.
- **Trazabilidad completa**: `WorkflowEjecucion` + `WorkflowPasoEjecucion` registran
  usuario, fechas, estado, resultado y tiempo de cada paso.
- **Extensibilidad**: el motor no conoce las herramientas concretas; agregar nuevas
  herramientas (ETAPAS futuras: eventos, programación) no requiere rediseñar el motor
  (solo registrar la herramienta en el *Tool Orchestrator* y referenciarla en un paso).
