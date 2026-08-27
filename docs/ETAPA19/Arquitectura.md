# ETAPA 19 — Arquitectura: Centro de Aprobaciones (Human-in-the-Loop)

## Diagrama de componentes

```
Usuario
  │  "Genera el reporte financiero y publícalo"
  ▼
Planner Engine
  │  PlanBuilder detecta acción sensible → paso Tipo=Approval
  ▼
Plan generado (DAG)
  │
  ▼
Approval Manager  ──────────────► ApprovalRequest (tabla)
  │  CrearSolicitudAsync            ├─ ApprovalAssignee (aprobadores)
  │  Pausa el plan (EnEsperaAprobacion) ─► ApprovalPolicy (reglas)
  │
  ▼  EsperarResolucionAsync (polling)
  │
  ▼
Aprobador (UI Bandeja)  ── DecidirAsync ──► ApprovalDecision (auditoría)
  │  Aprobar │ Rechazar │ Delegar
  ▼
Reanudar (plan → EnEjecucion)  │  Cancelar (plan → Cancelado)
  │
  ▼
Agent Orchestrator (omite nodos Approval, ejecuta Agent/Tool/RAG)
  │
  ▼
Resultado / Informe
```

## Capas (Clean Architecture)
- **Dominio**: `ApprovalRequest`, `ApprovalDecision`, `ApprovalAssignee`, `ApprovalPolicy`, enums.
- **Infraestructura**: EF Core (DbSets, configuraciones), repositorios, migración.
- **Aplicación**: `ApprovalManager` (lógica de ciclo de vida), integración con `PlannerEngine`.
- **API**: `AprobacionesController` (REST).
- **Web**: `AprobacionesController` + vistas (dashboard, bandeja).

## Integración con Planner y Orchestrator (RF punto 10 y 11)
- **Planner**: `EjecutarPlanAsync` detecta pasos `Approval` → crea la solicitud, pausa y espera.
- **Orchestrator**: `ExecutionGraphBuilder` marca nodos `EsAprobacion=true`; `EjecutarGrafoAsync`
  los omite (`Omitido`). La aprobación se resuelve en el Planner, NO se reconstruye el grafo.

## Ciclo de vida de una aprobación (máquina de estados)
```
Pendiente → EnRevision (parcial) → Aprobado ─┐
Pendiente → Rechazado ──────────────────────┼─► Plan Cancelado / Reanudado
Pendiente → Delegado ──(nuevo asignado)──► Pendiente
Pendiente → Expirado ───────────────────────┘
```

## Decisiones de diseño
- **No reconstruir el grafo**: el Orchestrator omite pasos Approval; el Planner los resuelve con
  `ApprovalManager` y luego delega el grafo (sin esos nodos).
- **Reanudación desde donde quedó**: al aprobar, el plan vuelve a `EnEjecucion` y el Orchestrator
  corre el grafo completo (los pasos ya ejecutados se reejecutan en CPU; en esta versión no se
  persiste el punto exacto de reanudación dentro del grafo, pero el plan conserva el estado).
- **Trazabilidad**: toda decisión/comentario/delegación se registra en `ApprovalDecision` y en
  `PlanExecutionLog`.

## Requisitos no funcionales (RF punto 16)
Clean Architecture, SOLID, asíncrono, Event Driven (PlanExecutionLog), alta trazabilidad, baja
dependencia (ApprovalManager es componente aislado), recuperación automática (reintentos del Planner).
