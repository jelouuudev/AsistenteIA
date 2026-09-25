# Guion Video Demostrativo — ETAPA 18: Planner Engine

**Duración estimada:** 12–15 min. **Requisito previo:** Docker up (`docker compose --profile prod up -d`)
y dashboard de planes en 0 (borrar planes previos para empezar limpio).

## Paso 1 — Generación automática de planes (RF §19.1)
- Entrar a Menú → **Agent Orchestrator** → **Planner Engine**.
- En "Generar plan desde lenguaje natural" escribir:
  > *"Analiza las ventas del trimestre, compara el cumplimiento con el procedimiento documentado,
  > genera un informe ejecutivo en PDF y resalta los riesgos detectados."*
- Pulsar **Generar plan**. Mostrar que aparece `Plan #N` en estado `Borrador`/`Validado` con varios pasos.

## Paso 2 — Simulación antes de ejecutar (RF §19.2)
- En el Detalle del plan, pulsar **🔍 Simular**. Explicar que se muestra el DAG sin consumir recursos.
- Señalar los pasos: `Coordination` → `Tool SqlQueryTool` + `RAG` (paralelo) → `Tool ReportTool` → entrega.

## Paso 3 — Validación (RF §19.3)
- Mostrar que el plan ya pasó `PlanValidator` (permisos y auth de agentes/herramientas, sin ciclos).
- Opcional: abrir `POST /api/planner/validar/{id}` en Swagger y mostrar `Valido: true`.

## Paso 4 — Ejecución (RF §19.4)
- Pulsar **▶ Ejecutar**. Explicar que cada paso validado lo ejecuta el Agent Orchestrator
  (`EjecutarPasoValidadoAsync`), sin re-seleccionar: el DAG validado es el que corre.
- La página se auto-refresca cada 10 s. Mostrar `EnEjecucion` → `Completado` y el resultado por paso.

## Paso 5 — Ramas paralelas (RF §19.5)
- Mostrar en el Detalle que SQL y RAG corrieron en paralelo (misma capa) y el reporte esperó a ambos.
- Explicar que el Planner descompuso el objetivo y ejecutó las ramas independientes a la vez.

## Paso 6 — Cancelación real (RF §19.6)
- Generar un plan nuevo y pulsar **▶ Ejecutar**.
- Antes de que termine, pulsar **⛔ Cancelar**. Mostrar que el plan pasa a `Cancelado`, el trabajo
  activo se detiene (CTS) y se registra en auditoría.

## Paso 7 — Reintentos (RF §19.7)
- Explicar `ExecutionSupervisor`: si un paso falla (ej. Ollama caído), reintenta hasta `MaxReintentos`
  y luego marca `Error`. Mostrar en auditoría los eventos `PasoReintentado`/`PasoError`.
- Demo en vivo: `docker stop asistenteollama` durante la ejecución de un plan → el paso queda `Error`
  y el plan `Fallido` (sin romper el sistema, EstrategiaError='Continuar'). `docker start asistenteollama`.

## Paso 8 — Dashboard (RF §19.8)
- Volver a **Planner Engine** (dashboard). Mostrar tarjetas Total/Activos/Finalizados/Fallidos y
  tiempo promedio, y la lista de planes (por usuario; Admin ve todos).

## Paso 9 — Auditoría (RF §19.9)
- En el Detalle de un plan, sección **Auditoría (PlanExecutionLog)**: evento, detalle, fecha.
- Explicar que cumple Regla 5 (planes auditables) con traza propia por paso.

## Paso 10 — Visualización gráfica del plan (RF §19.10)
- Recargar el Detalle y mostrar el **DAG** completo: nodos con tipo/estado/resultado y aristas de
  dependencia. Cerrar resaltando la evolución: *"de coordinar agentes → planificar antes de actuar"*.

## Criterio de aceptación (RF §21) — frase clave a leer al final
> *"Analiza las ventas del trimestre, compara el cumplimiento con el procedimiento documentado, genera
> un informe ejecutivo en PDF y resalta los riesgos detectados."*
Mostrar que el sistema: (1) construye el plan, (2) identifica agentes, (3) determina herramientas,
(4) consulta SQL y RAG en paralelo, (5) ejecuta el análisis, (6) genera el informe, (7) registra la
ejecución y (8) entrega el resultado final.

## Definition of Done (RF §20) — checklist al cierre
Plan generado ✓ · validado ✓ · grafo propio ejecutable ✓ · paralelismo real ✓ · trazabilidad ✓ ·
reintentos ✓ · cancelación real ✓ · simulación ✓ · pruebas satisfactorias ✓ (211 passed).
