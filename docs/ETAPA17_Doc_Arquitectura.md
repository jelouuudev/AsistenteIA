# Documento de Arquitectura — ETAPA 17: Agent Orchestrator

## Diagrama de componentes

```
 Usuario
   │  (pregunta + checkbox "Permitir colaboración multi-agente")
   ▼
 Chat (MVC) ──► ChatController ──► IChatService (Agent Runtime)
                                  │  si PermitirColaboracionMultagente
                                  ▼
                         IAgentOrchestrator
                          │
          ┌───────────────┼────────────────┐
          ▼               ▼                ▼
   IAgentSelector   IContextManager   IResponseAggregator
          │               │                │
          ▼               ▼                ▼
   AgentCollaboration  SharedContext    ExecutionGraph
   Rule Repository                     (topología)
          │
          ▼
   Execution Graph ──► capas (paralelo en capa / secuencial entre capas)
          │
          ▼  por cada nodo: IChatService.ProcesarMensajeAsync(agente)
          │
          ▼
   Consolidación ──► AgentExecutionResult (1 respuesta)
          │
          ▼
   Auditoría: AgentExecution + AgentExecutionStep + AgentExecutionTrace
```

## Capas (Clean Architecture)

- **Domain** (`Asistente.Domain`): entidades `AgentExecution`, `AgentExecutionStep`, `AgentCollaborationRule`,
  `AgentExecutionTrace`; límites en `ConfiguracionOrchestrator`.
- **Application** (`Asistente.Application`): interfaces `IAgentOrchestrator`, `IAgentSelector`,
  `IResponseAggregator`, `IContextManager`; servicios `AgentOrchestrator`, `AgentSelector`, `ContextManager`,
  `ResponseAggregator`; `ExecutionGraph`; DTOs en `Asistente.Shared`.
- **Infrastructure** (`Asistente.Infrastructure`): repos `AgentExecutionRepository`, `AgentExecutionStepRepository`,
  `AgentExecutionTraceRepository`, `AgentCollaborationRuleRepository`; EF configurations; migración.
- **API** (`Asistente.API`): `OrchestratorController` (execute, dashboard, trazas, reglas, agentes).
- **Web** (`Asistente.Web`): `OrchestratorController` MVC (Index, Trazas, Reglas) + checkbox en Chat.

## Patrones aplicados

- **Repository Pattern**: repos de auditoría desacoplados.
- **Dependency Injection**: todo registrado en los módulos de DI.
- **DTO /Interfaces**: contratos limpios entre capas.
- **Execution Graph**: resolución topológica para paralelismo y anti-ciclos.
- **Single Responsibility**: Selector (solo selección), Aggregator (solo consolidación), ContextManager (solo contexto).

## Integración con componentes existentes

- **Agent Runtime** = `IChatService` (reutilizado para ejecutar cada agente de forma aislada).
- **RAG / Tool Orchestrator / Workflow Engine / Event Engine**: se invocan indirectamente a través del
  Agent Runtime cuando un agente colaborador los usa.
- **Regla 1 cumplida**: ningún agente invoca a otro; el Orchestrator es el único mediador.
