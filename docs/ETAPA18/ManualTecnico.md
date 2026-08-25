# Manual Técnico — ETAPA 18: Planner Engine y Ejecución Autónoma Controlada

## 1. Objetivo
Implementar un componente **Planner Engine** que analiza solicitudes complejas en lenguaje natural,
genera un **plan de ejecución estructurado**, lo valida y lo delega al **Agent Orchestrator** (ETAPA 17)
para su ejecución multi-agente, manteniendo permisos, auditoría y gobernanza.

## 2. Arquitectura (Clean Architecture)
```
Usuario (Web)
   │  POST /api/planner/generar
   ▼
PlannerController (API)
   ▼
IPlannerEngine ──────────────► PlanBuilder (reglas + Ollama razonamiento)
   │                           PlanValidator (permisos, ciclos, agentes)
   │                           ExecutionGraphBuilder (DAG)
   │                           ExecutionSupervisor (tiempo, reintentos, cancelación)
   ▼
IPlanRepository / IPlanStepRepository / IPlanDependencyRepository / IPlanExecutionLogRepository
   ▼ (EF Core → SQL Server)
IAgentOrchestrator.ExecuteAsync  ──► AgentExecution (ETAPA 17)
```

## 3. Modelo de datos (4 tablas nuevas)
| Tabla | Propósito |
|-------|-----------|
| `Plan` | Plan maestro: Objetivo, Estado, RequiereAprobacion, Aprobado, IdExecution (vincula con ETAPA 17), TiempoTotalMs, Razonamiento. |
| `PlanStep` | Paso individual: Orden, Tipo (Agent/Tool/Workflow/RAG/Validation/Approval), Nombre, Descripcion, Estado, Resultado, IdAsistente, CodigoHerramienta, Intentos. |
| `PlanDependency` | Arista dirigida StepOrigen→StepDestino (forma el DAG). |
| `PlanExecutionLog` | Auditoría: Evento, Detalle, Fecha por cada plan. |

Estados de `Plan`: `Borrador` → `Validado` → `EnEjecucion` → `Completado` / `Fallido` / `Cancelado`.

## 4. Componentes internos (Application/Orchestrator/Planner)
- **PlanBuilder**: detecta intenciones por palabras clave (ventas/SQL, manual/RAG, resumen/PDF, riesgo,
  eliminar/enviar → Approval). Asigna agente principal (COMERCIAL-01) y colaboradores según tipo.
  Opcionalmente invoca Ollama (deepseek-r1:7b) para generar `Razonamiento` (timeout 20s, fallback silencioso).
- **PlanValidator**: verifica que cada `IdAsistente` exista y esté activo, que las herramientas estén
  asociadas al agente, y **detección de ciclos** (DFS) para cumplir Regla 7 (no grafos con ciclos).
- **ExecutionGraphBuilder**: mapea `PlanStep`→`ExecutionNode` y `PlanDependency`→`DependeDe` (reusa el
  `ExecutionGraph` de la ETAPA 17).
- **ExecutionSupervisor**: persiste inicio/fin, maneja reintentos (`MaxReintentos`, `TiempoEntreIntentosMs`),
  y marca pasos en `Error` al agotarlos. Permite cancelación (Estado=`Cancelado`).

## 5. Reglas de negocio implementadas
1. Todo plan se valida antes de ejecutarse (`ValidarPlanAsync`).
2. Ningún plan salta controles de permisos (validación de agentes/herramientas).
3. El Planner **nunca** ejecuta acciones directamente → delega al Orchestrator (`EjecutarPlanAsync`).
4. Toda ejecución pasa por `IAgentOrchestrator.ExecuteAsync` (Regla 4).
5. Planes auditables vía `PlanExecutionLog`.
6. Cancelación soportada (`Cancelar`).
7. Sin ciclos (DAG garantizado por el validador).
8. Cada acción tiene responsable (`IdAsistente` en cada paso).

## 6. API endpoints (`PlannerController`)
| Método | Ruta | Descripción |
|--------|------|-------------|
| POST | `/api/planner/generar` | Genera plan desde objetivo (lenguaje natural). |
| POST | `/api/planner/validar/{id}` | Valida plan (permisos, ciclos, agentes). |
| GET  | `/api/planner/simular/{id}` | Devuelve estructura del plan (visualización previa). |
| POST | `/api/planner/ejecutar/{id}` | Valida + ejecuta vía Orchestrator (fire-and-forget). |
| POST | `/api/planner/aprobar/{id}` | Aprueba plan que requiere aprobación humana. |
| POST | `/api/planner/cancelar/{id}` | Cancela plan en ejecución. |
| GET  | `/api/planner/dashboard` | Métricas: total/activos/finalizados/fallidos/tiempo promedio. |
| GET  | `/api/planner/{id}` | Detalle (pasos, dependencias, logs, IdExecution). |

## 7. Integración con ETAPA 17
`EjecutarPlanAsync` construye un `AgentRequest` con el agente principal (paso tipo `Agent`) y la pregunta
original del objetivo, y llama `IAgentOrchestrator.ExecuteAsync`. El `IdExecution` devuelto se guarda en
`Plan.IdExecution`, vinculando el plan con la traza del Orchestrator (auditoría completa).

## 8. Pruebas
- **Unitarias (7)**: `PlannerTests.cs` — detección por intención, aprobación por acción sensible,
  validación positiva/agente inexistente/ciclo, mapeo a grafo, reintentos agotados.
- **Suite completa**: 197 passed / 0 failed.
- **Integración**: el `PlannerEngine` usa los mismos repos que el Orchestrator (sin acoplamiento a infra).

## 9. Despliegue
- Migración EF: `20260825002802_Etapa18PlannerEngine` (script: `etapa18_migration.sql`).
- Aplicada a la BD Docker (`AsistenteIA`) vía sqlcmd.
- `docker compose --profile prod up -d --build api web`.
