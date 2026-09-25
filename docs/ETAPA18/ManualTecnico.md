# Manual Técnico — ETAPA 18: Planner Engine y Ejecución Autónoma Controlada

> Nota de vigencia: el Planner valida y gobierna el DAG; cada paso validado lo ejecuta
> el Agent Orchestrator sin re-seleccionar (fuente única de verdad, B-01).

## 1. Objetivo
Implementar un componente **Planner Engine** que analiza solicitudes complejas en lenguaje natural,
genera un **plan de ejecución estructurado**, lo valida y lo **ejecuta directamente** sobre su propio
grafo (capas secuenciales, nodos en paralelo), manteniendo permisos, auditoría y gobernanza.

## 2. Arquitectura (Clean Architecture)
```
Usuario (Web)
   │  POST /api/planner/generar
   ▼
PlannerController (API, con ownership: dueño o Administrador)
   ▼
IPlannerEngine ──────────────► PlanBuilder (reglas + Ollama razonamiento)
   │                           PlanValidator (permisos, auth B-04, ciclos, agentes, workflows)
   │                           ExecutionGraphBuilder (DAG, capas + Task.WhenAll)
   │                           ExecutionSupervisor (tiempo, reintentos, cancelación)
   ▼
IPlanRepository / IPlanStepRepository / IPlanDependencyRepository / IPlanExecutionLogRepository
   ▼ (EF Core → SQL Server)
Ejecución propia del grafo: Agent (ChatService) · Tool (orquestador) ·
Workflow (IWorkflowEngine) · RAG (recuperación)
```

## 3. Modelo de datos (4 tablas nuevas)
| Tabla | Propósito |
|-------|-----------|
| `Plan` | Plan maestro: Objetivo, Estado, RequiereAprobacion, Aprobado, IdUsuario (ownership), TiempoTotalMs, Razonamiento. |
| `PlanStep` | Paso individual: Orden, Tipo (Agent/Tool/Workflow/RAG/Validation/Approval), Nombre, Descripcion, Estado, Resultado, IdAsistente, CodigoHerramienta, **IdWorkflow**, Intentos. |
| `PlanDependency` | Arista dirigida StepOrigen→StepDestino (forma el DAG). |
| `PlanExecutionLog` | Auditoría: Evento, Detalle, Fecha por cada plan. |

Estados de `Plan`: `Borrador` → `Validado` → `EnEjecucion` → `Completado` / `Fallido` / `Cancelado`.

## 4. Componentes internos (Application/Orchestrator/Planner)
- **PlanBuilder**: detecta intenciones por palabras clave (ventas/SQL, manual/RAG, resumen/PDF, riesgo,
  eliminar/enviar → Approval, flujo/workflow → paso Workflow con `IdWorkflow` resuelto por disparadores).
  Asigna agente principal y colaboradores. Opcionalmente invoca Ollama (deepseek-r1:7b) para generar
  `Razonamiento` (timeout 20s, fallback silencioso).
- **PlanValidator**: verifica que cada `IdAsistente` exista y esté activo, autorización usuario→rol→
  agente→herramienta/workflow con política (B-04), workflows referenciados activos y permitidos,
  y **detección de ciclos** (DFS) para cumplir Regla 7 (no grafos con ciclos).
- **ExecutionGraphBuilder**: mapea `PlanStep`→`ExecutionNode` y `PlanDependency`→`DependeDe`; las capas
  se ejecutan en secuencia y los nodos de cada capa en paralelo (`Task.WhenAll`).
- **ExecutionSupervisor**: persiste inicio/fin, maneja reintentos (`MaxReintentos`, `TiempoEntreIntentosMs`),
  y marca pasos en `Error` al agotarlos. Cancelación real vía CTS registrado por plan (B-03).

## 5. Reglas de negocio implementadas
1. Todo plan se valida antes de ejecutarse (`ValidarPlanAsync`).
2. Ningún plan salta controles de permisos (validación de agentes/herramientas/workflows, B-04).
3. El Planner ejecuta su propio grafo validado; no reconstruye ni delega planes.
4. Ownership: solo el dueño del plan o un Administrador pueden verlo/operarlo; el dashboard es por usuario.
5. Planes auditables vía `PlanExecutionLog`.
6. Cancelación real: el endpoint invoca `Cancel()` sobre la ejecución activa (B-03).
7. Sin ciclos (DAG garantizado por el validador).
8. Cada acción tiene responsable (`IdAsistente` en cada paso).

## 6. API endpoints (`PlannerController`)
| Método | Ruta | Descripción |
|--------|------|-------------|
| POST | `/api/planner/generar` | Genera plan desde objetivo (lenguaje natural). |
| POST | `/api/planner/validar/{id}` | Valida plan (permisos, auth, ciclos, agentes). Dueño/Admin. |
| GET  | `/api/planner/simular/{id}` | Devuelve estructura del plan (visualización previa). Dueño/Admin. |
| POST | `/api/planner/ejecutar/{id}` | Valida + ejecuta el grafo propio (fire-and-forget). Dueño/Admin. |
| POST | `/api/planner/aprobar/{id}` | Aprueba plan que requiere aprobación humana. Dueño/Admin. |
| POST | `/api/planner/cancelar/{id}` | Detiene la ejecución activa + marca estado. Dueño/Admin. |
| GET  | `/api/planner/dashboard` | Métricas por usuario (Admin ve todas). |
| GET  | `/api/planner/{id}` | Detalle (pasos, dependencias, logs). Dueño/Admin. |

## 7. Integración con ETAPA 17
El Planner **ya no usa** el Agent Orchestrator para planes: los pasos `Agent` se ejecutan vía
`ChatService` con contexto de pasos previos, y los pasos `Workflow` vía `IWorkflowEngine`.
El Orchestrator (ETAPA 17) queda reservado a colaboración multi-agente ad-hoc desde el chat.

## 8. Pruebas
- **Unitarias**: `PlannerTests.cs` — detección por intención, aprobación por acción sensible,
  validación positiva/agente inexistente/ciclo, mapeo a grafo, reintentos agotados, selección de
  workflow con `IdWorkflow` (B-05).
- **Suite completa**: 211 passed / 0 failed.
- **Integración**: el `PlannerEngine` usa los mismos repos (sin acoplamiento a infra).

## 9. Despliegue
- Migración EF: `20260825002802_Etapa18PlannerEngine` (script: `etapa18_migration.sql`).
- Aplicada a la BD Docker (`AsistenteIA`) vía sqlcmd.
- `docker compose --profile prod up -d --build api web`.
