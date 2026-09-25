# Documento de Arquitectura — Workflow Engine (ETAPA 12)

## 1. Propósito

El **Workflow Engine** automatiza procesos empresariales multi-paso reutilizando el
*Tool Orchestrator* (ETAPA 11). Sigue **Clean Architecture**: la lógica de orquestación
vive en la capa **Application**, depende de abstracciones de **Domain** (repositorios,
entidades) e infraestructura vía **DI**, y se expone por **API** (REST) y **Web** (MVC).

## 2. Diagrama de capas

```
┌─────────────────────────────────────────────────────────────┐
│  Web (MVC, Bootstrap 5)         API (ASP.NET Core, REST)      │
│  WorkflowsController ──► IApiService ──► WorkflowsController   │
│  MonitoreoWorkflowsController      │   WorkflowEjecucionesCtrl  │
│                                    ▼                            │
│  Application (Servicios)                                          │
│  ┌─────────────────────────────────────────────────────────┐  │
│  │ ChatService ──► WorkflowDecisionService (match determinista)│  │
│  │        │                                                   │  │
│  │        ▼                                                   │  │
│  │ WorkflowEngine (IWorkflowEngine)                          │  │
│  │   • secuencia • contexto compartido • reintentos          │  │
│  │   • confirmación • auditoría                              │  │
│  │        │ Ejecuta cada paso vía                            │  │
│  │        ▼                                                   │  │
│  │ IToolOrchestrator (ETAPA 11) ──► ITool (Sql/Doc/Report/...) │  │
│  │ WorkflowService / ConfiguracionWorkflowService            │  │
│  └─────────────────────────────────────────────────────────┘  │
│                         │                                      │
│  Domain (Entities, Interfaces)  ◄── abstracciones              │
│  Infrastructure (EF Core, SQL Server, Repos)                    │
└─────────────────────────────────────────────────────────────┘
```

## 3. Flujo de ejecución (caso típico)

```
Usuario ─► Chat ─► ChatService
                 │
                 ├─ WorkflowDecisionService.DecidirAsync(mensaje)
                 │     → match por Workflow.Disparadores (determinista)
                 │
                 └─ WorkflowEngine.EjecutarAsync(idWorkflow, usuario, asistente)
                          │
                          ├─ Carga Workflow + Pasos (IWorkflowRepository)
                          ├─ Crea WorkflowEjecucion (Estado=EnProceso)
                          ├─ Por cada Paso (orden):
                          │     ├─ SustituirContexto({{resultado}}, etc.)
                          │     ├─ IToolOrchestrator.EjecutarAsync(paso)
                          │     ├─ Reintentos (hasta ReintentosMaximos)
                          │     ├─ Si falla → EstrategiaError (Omitir/Cancelar/Registrar)
                          │     ├─ Audita WorkflowPasoEjecucion
                          │     └─ Publica resultado en contexto compartido
                          ├─ Si paso RequiereConfirmacion && !confirmado:
                          │     → Estado=RequiereConfirmacion; retorna; espera "confirmar"
                          │       (reanudación vía ReanudarPendienteConfirmacionAsync)
                          └─ Estado Final = Exitosa / Error; ResultadoFinal + TiempoTotal
                                   │
                 ChatService inyecta ResultadoFinal en el prompt → DeepSeek presenta respuesta
```

## 4. Contexto compartido (Actividad 4)

`WorkflowEngine` mantiene `Dictionary<string,string> contexto` durante la ejecución.
Tras cada paso exitoso se indexa el resultado bajo tres claves:
- `Paso{N}` — por número de orden.
- `<CodigoHerramienta>` — por código (ej. `SqlQueryTool`).
- `resultado` — **resultado del paso inmediatamente anterior** (para `{{resultado}}`).

`SustituirContexto` reemplaza los tokens en los parámetros JSON del paso siguiente,
permitiendo encadenar salidas (ej. Paso 1 consulta clientes → Paso 2 genera reporte con
`{{resultado}}`).

## 5. Reintentos y errores (Actividad 5)

`reintentos = max(paso.ReintentosMaximos, config.ReintentosMaximos)`. El bucle intenta
`reintentos + 1` veces con backoff. Al agotarlos, aplica `EstrategiaError`:
- `Omitir`: continúa con el siguiente paso. (`RegistrarIncidencia` se eliminó: era idéntica sin registrar nada.)
- `Cancelar` (default): marca la ejecución como **Error** y detiene.

## 6. Confirmación del usuario (Actividad 6)

Si `Paso.RequiereConfirmacion && !confirmado && ConfiguracionWorkflow.ConfirmacionesObligatorias`,
el motor persiste `WorkflowEjecucion.Estado = "RequiereConfirmacion"` y retorna. El
`ChatService` devuelve `RequiereConfirmacionWorkflow=true` y un mensaje. El usuario responde
*confirmar*; `ChatService` invoca `ReanudarPendienteConfirmacionAsync`, que reanuda la misma
`WorkflowEjecucion` (salta pasos ya Exitosa) con `confirmado=true` y continúa.

## 7. Auditoría (Actividad 7)

Toda ejecución queda registrada:
- `WorkflowEjecucion`: usuario, asistente, inicio/fin, estado, tiempo total, resultado.
- `WorkflowPasoEjecucion`: por cada paso — fecha, resultado, estado, observaciones.

Esto alimenta el **panel de monitoreo** (Actividad 8) y cumple la trazabilidad requerida.

## 8. Desacoplamiento y extensibilidad

- El motor **no** instancia herramientas; delega en `IToolOrchestrator`, por lo que hereda
  autorización y auditoría de herramientas sin acoplamiento.
- Agregar una nueva herramienta (futuras ETAPAS: eventos de negocio, integraciones externas,
  ejecución programada) **no** requiere rediseñar el motor: basta registrarla en el orquestador
  y referenciarla desde un paso. Esto satisface el criterio de aceptación de preparación para
  extensiones sin reingeniería.

## 9. Decisiones de diseño

| Decisión | Justificación |
|---|---|
| Match determinista (disparadores) en lugar de LLM | Evita latencia/dependencia del modelo para el demo; es predecible y auditable. |
| Reanudación por `idEjecucionExistente` | Permite confirmación en dos turnos del chat sin perder el progreso ni duplicar auditoría. |
| Contexto compartido en `Dictionary<string,string>` | Simple, serializable y suficiente para encadenar salidas de herramientas. |
| Workflows globales (disponibilidad por `Estado`) | Satisface "reutilizar entre asistentes" sin tabla de asociación adicional. |
