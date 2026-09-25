# Arquitectura — ETAPA 18: Planner Engine (estado actual)

> Nota de vigencia: el Planner valida y gobierna el DAG; cada paso validado lo ejecuta
> el Agent Orchestrator (`EjecutarPasoValidadoAsync`) sin re-seleccionar ni reconstruir
> (fuente única de verdad, B-01). El flujo multi-agente ad-hoc desde el chat sigue aparte.

## Diagrama de componentes
```
                 Usuario (Web: /Planner)
                          │
                          ▼
                   PlannerController (API)
                          │
                          ▼
                 ┌──────────────────┐
                 │   PLANNER ENGINE  │
                 │  (IPlannerEngine) │
                 └────────┬─────────┘
            ┌─────────────┼──────────────────┐
            ▼             ▼                  ▼
      ┌──────────┐ ┌────────────┐   ┌──────────────┐
      │PlanBuilder│ │PlanValidator│   │ExecutionGraph│
      │(reglas+LLM)│ │(permisos,   │   │  Builder(DAG) │
      │           │ │ auth, ciclos)│   │              │
      └─────┬─────┘ └─────┬──────┘   └──────┬───────┘
            └─────────────┼─────────────────┘
                          ▼
                 ┌──────────────────┐
                 │ ExecutionGraph   │  capas secuenciales,
                 │ (capas +         │  nodos por capa en paralelo
                 │  Task.WhenAll)   │  (Task.WhenAll)
                 └────────┬─────────┘
              ┌───────────┼────────────┬────────────┐
              ▼           ▼            ▼            ▼
          Agentes      Tools      Workflows      RAG
          (ChatService)(orquestador)(WorkflowEngine)(Recuperacion)
              │
              ▼
        PlanStep.Resultado + PlanExecutionLog (auditoría propia)
              │
              ▼
        Resultado consolidado
```

## Capas (Clean Architecture / SOLID)
- **Domain** (`Asistente.Domain`): entidades `Plan`, `PlanStep`, `PlanDependency`, `PlanExecutionLog`
  (POCO, sin dependencias de infra).
- **Application** (`Asistente.Application`):
  - Interfaces: `IPlannerEngine`, `IPlanRepository*`, DTOs en `Asistente.Shared`.
  - Servicios: `Planner/PlanBuilder`, `PlanValidator`, `ExecutionGraphBuilder`, `ExecutionSupervisor`,
    `PlannerEngine` (orquestador). Dependen de abstracciones (repos, `IToolOrchestrator`,
    `IWorkflowEngine`, `IOllamaService`).
- **Infrastructure** (`Asistente.Infrastructure`): repos EF Core, configuraciones de tablas, DbContext.
- **API** (`Asistente.API`): `PlannerController` (HTTP, fire-and-forget para ejecución lenta).
- **Web** (`Asistente.Web`): `PlannerController` + vistas `Index` (dashboard), `Detalle` (visualizador),
  nav en `_Layout`.

## Patrones aplicados
- **Dependency Inversion**: todo depende de interfaces; DI en `Application/DependencyInjection.cs`
  y `Infrastructure/DependencyInjection.cs`.
- **Fire-and-forget**: la ejecución del plan (lenta en CPU) corre en `Task.Run` con scope propio,
  devolviendo el `IdPlan` de inmediato.
- **DAG Execution Model**: `PlanDependency` forma un grafo acíclico validado por DFS; las capas se
  ejecutan en secuencia y los nodos de cada capa en paralelo (`Task.WhenAll`).
- **Repository**: acceso a datos aislado; el Planner no conoce EF Core.
- **Observer/Supervisor**: `ExecutionSupervisor` monitorea tiempo, errores y reintentos.
- **Cancelación real (B-03)**: CTS registrado por `IdPlan`; el endpoint Cancelar invoca `Cancel()`.

## Flujo de datos (un plan)
1. `POST /api/planner/generar {objetivo}` → `PlannerEngine.GenerarPlanAsync`
   → `PlanBuilder.ConstruirAsync` (reglas + Ollama razonamiento) → `PlanRepository.AddAsync`.
   Si el objetivo menciona un flujo, el builder resuelve el workflow activo por disparadores
   y persiste su `IdWorkflow` en el paso (B-05).
2. `POST /api/planner/validar/{id}` → `PlanValidator.ValidarAsync` (agentes, herramientas con
   autorización B-04, workflows y política, ciclos). Solo dueño o Administrador.
3. `POST /api/planner/ejecutar/{id}` → `PlannerEngine.EjecutarPlanAsync` → `PlanValidator`
   → `ExecutionGraphBuilder.Construir` → cada paso validado lo ejecuta el Agent Orchestrator
   (`EjecutarPasoValidadoAsync`: Agent vía ChatService, Tool/RAG vía ToolOrchestrator,
   Workflow vía `IWorkflowEngine`). Solo dueño o Admin.
4. `GET /api/planner/{id}` → detalle con pasos, dependencias y logs. Solo dueño o Admin.
5. `POST /api/planner/cancelar/{id}` → `CancelarEjecucionAsync`: detiene el trabajo activo (B-03).
6. `GET /api/planner/dashboard` → métricas del usuario (Administrador ve todas).

## Trazabilidad completa (Req. no funcionales §15)
Cada plan queda registrado en 4 tablas (`Plan`, `PlanStep`, `PlanDependency`, `PlanExecutionLog`)
más los resultados por paso, cumpliendo auditoría (Regla 5), cancelación real (Regla 6),
sin ciclos (Regla 7) y responsable identificado (Regla 8).
