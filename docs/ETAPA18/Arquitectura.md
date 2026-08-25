# Arquitectura — ETAPA 18: Planner Engine

## Diagrama de componentes (objetivo del RF §4 + §14)
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
            ┌─────────────┼─────────────────┐
            ▼             ▼                  ▼
      ┌──────────┐ ┌────────────┐   ┌──────────────┐
      │PlanBuilder│ │PlanValidator│   │ExecutionGraph│
      │(reglas+LLM)│ │(permisos,   │   │  Builder(DAG) │
      │           │ │ ciclos,     │   │              │
      │           │ │ agentes)    │   │              │
      └─────┬─────┘ └─────┬──────┘   └──────┬───────┘
            └─────────────┼─────────────────┘
                          ▼
                 ┌──────────────────┐
                 │ExecutionSupervisor│ (tiempo, reintentos, cancelación)
                 └────────┬─────────┘
                          ▼
                 ┌──────────────────┐
                 │ Agent Orchestrator│ (ETAPA 17)
                 │  (ExecuteAsync)   │
                 └────────┬─────────┘
              ┌───────────┼────────────┬────────────┐
              ▼           ▼            ▼            ▼
          Agentes      Tools       Workflows      RAG
              │
              ▼
        AgentExecution (trazas, ETAPA 17)  ◄── Plan.IdExecution (vínculo auditoría)
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
    `PlannerEngine` (orquestador). Dependen de abstracciones (repos, `IAgentOrchestrator`, `IOllamaService`).
- **Infrastructure** (`Asistente.Infrastructure`): repos EF Core, configuraciones de tablas, DbContext.
- **API** (`Asistente.API`): `PlannerController` (HTTP, fire-and-forget para ejecución lenta).
- **Web** (`Asistente.Web`): `PlannerController` + vistas `Index` (dashboard), `Detalle` (visualizador),
  nav en `_Layout`.

## Patrones aplicados
- **Dependency Inversion**: todo depende de interfaces; DI en `Application/DependencyInjection.cs`
  y `Infrastructure/DependencyInjection.cs`.
- **Fire-and-forget**: la ejecución del plan (lenta en CPU) corre en `Task.Run` con scope propio,
  devolviendo el `IdPlan` de inmediato (igual que ETAPA 17).
- **DAG Execution Model**: `PlanDependency` forma un grafo acíclico validado por DFS.
- **Repository**: acceso a datos aislado; el Planner no conoce EF Core.
- **Observer/Supervisor**: `ExecutionSupervisor` monitorea tiempo, errores y reintentos.

## Flujo de datos (un plan)
1. `POST /api/planner/generar {objetivo}` → `PlannerEngine.GenerarPlanAsync`
   → `PlanBuilder.ConstruirAsync` (reglas + Ollama razonamiento) → `PlanRepository.AddAsync`.
2. `POST /api/planner/validar/{id}` → `PlanValidator.ValidarAsync` (agentes, herramientas, ciclos).
3. `POST /api/planner/ejecutar/{id}` → `PlannerEngine.EjecutarPlanAsync`
   → `PlanValidator` → `ExecutionGraphBuilder.Construir` → `IAgentOrchestrator.ExecuteAsync`
   → guarda `Plan.IdExecution`.
4. `GET /api/planner/{id}` → detalle con pasos, dependencias, logs y `IdExecution`.

## Trazabilidad completa (Req. no funcionales §15)
Cada plan queda registrado en 4 tablas + las trazas del Orchestrator (ETAPA 17), cumpliendo
auditoría (Regla 5), cancelación (Regla 6), sin ciclos (Regla 7) y responsable identificado (Regla 8).
