# Guion Video Demostrativo — ETAPA 18: Planner Engine

**Duración estimada:** 12–15 min. **Requisito previo:** Docker up (`docker compose --profile prod up -d`),
reglas de colaboración de la ETAPA 17 creadas (Comercial→Reportes ✓, Soporte→Comercial ✓, Comercial→RRHH ✗,
Comercial→Soporte ✓), y dashboard de planes en 0 (borrar planes previos para empezar limpio).

## Paso 1 — Generación automática de planes (RF §19.1)
- Entrar a Menú → **Agent Orchestrator** → **Planner Engine**.
- En "Generar plan desde lenguaje natural" escribir:
  > *"Analiza las ventas del trimestre, compara el cumplimiento con el procedimiento documentado,
  > genera un informe ejecutivo en PDF y resalta los riesgos detectados."*
- Pulsar **Generar plan**. Mostrar que aparece `Plan #N` en estado `Borrador`/`Validado` con varios pasos.

## Paso 2 — Simulación antes de ejecutar (RF §19.2)
- En el Detalle del plan, pulsar **🔍 Simular**. Explicar que se muestra el DAG sin consumir recursos.
- Señalar los pasos: `Agente (Comercial)` → `Tool SqlQueryTool` → `RAG` → `Tool ReportTool` → `Validation`.

## Paso 3 — Validación (RF §19.3)
- Mostrar que el plan ya pasó `PlanValidator` (permisos de agentes/herramientas, sin ciclos).
- Opcional: abrir `POST /api/planner/validar/{id}` en Swagger y mostrar `Valido: true`.

## Paso 4 — Ejecución (RF §19.4)
- Pulsar **▶ Ejecutar**. Explicar que el plan se delega al **Agent Orchestrator** (ETAPA 17).
- La página se auto-refresca cada 10 s. Mostrar `EnEjecucion` → `Completado` y el `IdExecution` vinculado.

## Paso 5 — Colaboración de agentes (RF §19.5)
- Ir a **Agent Orchestrator → Trazas** con el `IdExecution` del plan.
- Mostrar `Candidatos seleccionados: Agente Soporte, Agente Reportes` (Comercial + colaboradores).
- Explicar que el Planner descompuso el objetivo y el Orchestrator coordinó los agentes.

## Paso 6 — Cancelación (RF §19.6)
- Generar un plan nuevo y pulsar **▶ Ejecutar**.
- Antes de que termine, pulsar **⛔ Cancelar**. Mostrar que el plan pasa a `Cancelado` y se registra en auditoría.

## Paso 7 — Reintentos (RF §19.7)
- Explicar `ExecutionSupervisor`: si un paso falla (ej. Ollama caído), reintenta hasta `MaxReintentos`
  y luego marca `Error`. Mostrar en auditoría los eventos `PasoReintentado`/`PasoError`.
- Demo en vivo: `docker stop asistenteollama` durante la ejecución de un plan → el paso queda `Error`
  y el plan `Fallido` (sin romper el sistema, EstrategiaError='Continuar'). `docker start asistenteollama`.

## Paso 8 — Dashboard (RF §19.8)
- Volver a **Planner Engine** (dashboard). Mostrar tarjetas Total/Activos/Finalizados/Fallidos y
  tiempo promedio, y la lista de planes.

## Paso 9 — Auditoría (RF §19.9)
- En el Detalle de un plan, sección **Auditoría (PlanExecutionLog)**: evento, detalle, fecha.
- Explicar que cumple Regla 5 (planes auditables) y vincula con la traza del Orchestrator.

## Paso 10 — Visualización gráfica del plan (RF §19.10)
- Recargar el Detalle y mostrar el **DAG** completo: nodos con tipo/estado/resultado y aristas de
  dependencia. Cerrar resaltando la evolución: *"de coordinar agentes → planificar antes de actuar"*.

## Criterio de aceptación (RF §21) — frase clave a leer al final
> *"Analiza las ventas del trimestre, compara el cumplimiento con el procedimiento documentado, genera
> un informe ejecutivo en PDF y resalta los riesgos detectados."*
Mostrar que el sistema: (1) construye el plan, (2) identifica agentes, (3) determina herramientas,
(4) consulta SQL, (5) consulta RAG, (6) ejecuta el análisis, (7) genera el informe, (8) registra la
ejecución y (9) entrega el resultado final.

## Definition of Done (RF §20) — checklist al cierre
Plan generado ✓ · validado ✓ · grafo ejecutable ✓ · Orchestrator ejecuta ✓ · trazabilidad ✓ ·
reintentos ✓ · cancelación ✓ · simulación ✓ · pruebas satisfactorias ✓ (197 passed).
