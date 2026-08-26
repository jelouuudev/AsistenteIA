# Manual Técnico — ETAPA 17: Agent Orchestrator y Colaboración entre Agentes

**Proyecto:** Asistente Inteligente Empresarial basado en IA Local (Versión 2.0)
**Fecha:** 2026-08-21
**Alcance:** Implementación del Agent Orchestrator que coordina múltiples agentes especializados sin llamadas directas entre ellos.

---

## 1. Objetivo

El **Agent Orchestrator** es un componente central que recibe una solicitud del usuario, identifica el agente
principal, selecciona colaboradores autorizados, construye un **Execution Graph**, ejecuta los nodos
(secuencial/paralelo), consolida los resultados en una única respuesta y registra trazabilidad completa.

## 2. Principios de arquitectura (Reglas del RF)

| Regla | Implementación |
|-------|----------------|
| 1. Un agente nunca invoca directamente a otro | El Orchestrator es el único que ejecuta agentes vía `IChatService` (Agent Runtime). |
| 2. Toda colaboración pasa por el Orchestrator | `ChatService` rutea a `IAgentOrchestrator` cuando `PermitirColaboracionMultagente=true`. |
| 3. Solo colaboran agentes autorizados | `AgentCollaborationRule.EstaPermitidoAsync` (deny by default). |
| 4. Contexto compartido solo si hay autorización | `ContextManager.BuildContextForAgentAsync` filtra por reglas. |
| 5. Limitar profundidad de ejecución | `ConfiguracionOrchestrator.MaxProfundidad` / `MaxAgentesPorSolicitud`. |
| 6. Toda colaboración queda auditada | `AgentExecution` / `AgentExecutionStep` / `AgentExecutionTrace`. |
| 7. Una sola respuesta consolidada | `ResponseAggregator.BuildFinalResponseAsync`. |

## 3. Modelo de datos (nuevas entidades)

- **AgentExecution**: IdExecution, IdUsuario, IdAgentePrincipal, Pregunta, FechaInicio, FechaFin, Estado, TiempoTotalMs, CantidadAgentes, ProfundidadAlcanzada, HerramientasUtilizadas, RespuestaFinal, Error.
- **AgentExecutionStep**: IdStep, IdExecution, Orden, IdAgente, Accion, Resultado, TiempoMs, Estado, Dependencias (JSON).
- **AgentCollaborationRule**: IdRule, AgenteOrigen, AgenteDestino, Permitido, Prioridad, Activa, UsuarioCreacion, FechaCreacion.
- **AgentExecutionTrace**: IdTrace, IdExecution, Evento, Detalle, FechaHora.
- **ConfiguracionOrchestrator** (extendido): MaxAgentesPorSolicitud, MaxProfundidad, TiempoMaximoMs, EstrategiaError (Continuar/Reintentar/Cancelar).

## 4. Interfaces (desacopladas, SOLID)

```csharp
public interface IAgentOrchestrator { Task<AgentExecutionResult> ExecuteAsync(AgentRequest request); }
public interface IAgentSelector { Task<IEnumerable<AgentCandidate>> SelectAgentsAsync(AgentRequest request); }
public interface IResponseAggregator { Task<string> BuildFinalResponseAsync(AgentExecution execution); }
public interface IContextManager { Task<AgentContext> BuildContextForAgentAsync(int idAgente, SharedContext ctx); bool PuedeCompartir(int o, int d); }
```

## 5. Execution Graph (mejora arquitectónica recomendada)

Cada nodo = una tarea (invocar agente / Tool / RAG). `ExecutionGraph.ObtenerCapas()` resuelve la
topología: agrupa en capas para **paralelizar tareas independientes** y procesar **secuencialmente entre capas**.
Lanza `InvalidOperationException` si detecta **dependencias circulares** (prevención de ciclos).

## 6. Flujo de ejecución (Híbrido, opción elegida)

1. Usuario marca "Permitir colaboración multi-agente" en el chat (o usa `/api/orchestrator/execute`).
2. `ChatService` construye `AgentRequest` y llama `AgentOrchestrator.ExecuteAsync`.
3. El Orchestrator: crea `AgentExecution`, selecciona colaboradores (`AgentSelector`), construye el grafo,
   ejecuta por capas (paralelo dentro de capa), consolida (`ResponseAggregator`) y registra trazas.
4. Devuelve `AgentExecutionResult` (respuesta única + pasos + trazas).

## 7. Control de profundidad y errores (Actividades 8 y 9)

- `MaxAgentesPorSolicitud` limita colaboradores; `MaxProfundidad` aborta grafos muy profundos.
- `EstrategiaError`: `Continuar` (omite nodo fallido), `Reintentar` (reintenta solo ese nodo), `Cancelar` (aborta).
- Todo se registra en `AgentExecutionTrace`.

## 8. Capa de persistencia

- Migración EF: `20260822151847_Etapa17AgentOrchestrator`. Aplicar con `dotnet ef database update`
  (o SQL generado en `etapa17_apply.sql` dentro del contenedor `asistentesql`).

## 9. Inyección de dependencias

Registrado en `Asistente.Application/DependencyInjection.cs` y `Asistente.Infrastructure/DependencyInjection.cs`:
`IAgentOrchestrator→AgentOrchestrator`, `IAgentSelector→AgentSelector`, `IContextManager→ContextManager`,
`IResponseAggregator→ResponseAggregator`, repos `IAgentExecution*Repository`, `IAgentCollaborationRuleRepository`.

## 10. API y MVC

- **API**: `OrchestratorController` (`/api/orchestrator/execute`, `/dashboard`, `/trazas/{id}`, `/reglas` CRUD, `/agentes`).
- **MVC**: `OrchestratorController` (vistas `Index`, `Trazas`, `Reglas`) + checkbox en `Chat/Index.cshtml`.

## 11. Pruebas

- Unitarias (xUnit + Moq): `AgentOrchestratorTests` (ExecutionGraph, ResponseAggregator, ContextManager),
  `AgentSelectorOrchestratorIntegrationTests` (selección por reglas + orquestación end-to-end).
- Total suite: 188 pruebas en verde.
