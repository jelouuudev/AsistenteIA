# Manual de Usuario — ETAPA 18: Planner Engine

## ¿Qué es?
El **Planner Engine** convierte una instrucción en lenguaje natural (por ejemplo: *"Analiza las ventas
del trimestre y genera un informe"*) en un **plan estructurado** de pasos, y lo ejecuta sobre su propio
grafo (capas en secuencia, pasos independientes en paralelo). Ya no escribes la consulta directa:
explicas el **objetivo** y el sistema planifica por ti (Regla de evolución del RF: *"desde coordinar
agentes hacia planificar antes de actuar"*).

## Acceso
Menú lateral → **Agent Orchestrator** (grupo) → **Planner Engine**.
Requiere rol `Administrador`, `Operador` o `Supervisor`.
Ves tus propios planes; el Administrador ve todos.

## Flujo paso a paso
1. **Dashboard** (`/Planner`): ves tarjetas con Total / Activos / Finalizados / Fallidos y tiempo
   promedio, más tu lista de planes recientes.
2. **Generar plan**: en el recuadro "Generar plan desde lenguaje natural" escribes el objetivo y
   pulsas **Generar plan**. El sistema crea el plan en estado `Borrador` y lo valida automáticamente.
   Si mencionas un flujo ("automatizar el reporte de clientes"), el plan incluye el paso Workflow
   correspondiente si existe y está activo.
3. **Visualizador (Detalle)**: muestra el **DAG** (grafo de ejecución) con cada paso, su tipo
   (Agente/Herramienta/Workflow/RAG/Validación/Aprobación), dependencias y estado. También el *Razonamiento*
   que el Planner registró.
4. **Simular**: botón "🔍 Simular" — muestra el plan tal como se ejecutará, **sin** lanzarlo. Útil
   para revisar antes de consumir recursos (Actividad 5).
5. **Aprobar** (si aplica): si el plan contiene acciones sensibles (eliminar, enviar, pagar) aparece
   "✓ Aprobar". Debes aprobarlo antes de ejecutar (aprobación humana opcional — Sección 12).
6. **Ejecutar**: "▶ Ejecutar" lanza el grafo propio del Planner. La página se auto-refresca cada
   10 s mientras está `EnEjecucion`. Al terminar muestra `Completado` y el resultado por paso.
7. **Cancelar**: durante la ejecución puedes pulsar "⛔ Cancelar" (Regla 6 / Actividad 8). Detiene
   el trabajo activo de verdad, no solo cambia el estado.
8. **Auditoría**: sección inferior "Auditoría (PlanExecutionLog)" con evento, detalle y fecha de cada
   acción del plan (Regla 5 / Sección 13).

## Ejemplo del RF (Criterio de aceptación)
Objetivo:
> *"Analiza las ventas del trimestre, compara el cumplimiento con el procedimiento documentado, genera
> un informe ejecutivo en PDF y resalta los riesgos detectados."*

El Planner generará pasos tipo: `Coordination` → `Tool SqlQueryTool` + `RAG` (en paralelo) →
`Tool ReportTool` → `Agent` de entrega, con dependencias reales. Al ejecutar, cada rama corre en su
orden y el informe se consolida al final.

## Troubleshooting
- **Plan no ejecuta / vacío**: verifica que los agentes tengan las herramientas asociadas activas
  y que las tablas estén autorizadas en la conexión correspondiente.
- **Estado `EnEjecucion` muy largo**: DeepSeek-r1:7b en CPU tarda 40–300 s por agente; el plan puede
  tardar varios minutos. No canceles salvo que quieras abortar.
- **Acción sensible sin aprobar**: el botón Ejecutar solo aparece tras Aprobar.
- **403 en un plan**: solo el dueño o un Administrador pueden verlo/operarlo.
