# Guion de Video Demostrativo — ETAPA 17: Agent Orchestrator

**Duración estimada:** 8-10 min · **Rol sugerido en cámara:** Administrador (para administrar reglas).

---

## Escena 1 — Creación de reglas de colaboración (00:00-01:00)
- Ir a menú → **Agent Orchestrator (ETAPA 17)** → pestaña **Reglas**.
- Crear: `Comercial → Reportes = Permitido`.
- Crear: `Soporte → Comercial = Permitido`.
- Crear: `Comercial → RRHH = Denegado` (demostrar bloqueo).
- Mostrar la lista de reglas activas.

## Escena 2 — Solicitud con dos agentes (01:00-02:30)
- En **Agent Orchestrator** (Index): Agente principal = Comercial.
- Pregunta: "Muéstrame las ventas del mes y prepárame un resumen."
- Ejecutar. Mostrar respuesta consolidada (SQL de Comercial + Resumen de Reportes).

## Escena 3 — Solicitud con tres agentes (02:30-04:00)
- Agente principal = Comercial.
- Pregunta (criterio de aceptación): "Analiza las ventas del mes, prepara un resumen ejecutivo y
  compáralo con el procedimiento del manual."
- Ejecutar. Resaltar: Comercial (SQL) + Soporte (RAG) + Reportes (resumen) coordinados.

## Escena 4 — Ejecución paralela (04:00-05:00)
- En el visualizador de trazas: mostrar que Comercial y Soporte corren en la **misma capa** (paralelo)
  y Reportes en la **capa siguiente** (secuencial, usa resultados previos).

## Escena 5 — Ejecución secuencial (05:00-05:30)
- Explicar el grafo: dependencias = Reportes depende de Comercial+Soporte.

## Escena 6 — Dashboard (05:30-06:30)
- Sección Dashboard: total, completadas, con error, tiempo promedio, lista de ejecuciones.

## Escena 7 — Trazabilidad (06:30-07:30)
- Abrir **Trazas** de la ejecución de 3 agentes. Mostrar eventos: Inicio, SeleccionAgentes,
  EjecucionNodo x3, Consolidacion, Fin.

## Escena 8 — Auditoría (07:30-08:00)
- Mencionar que todo queda en `AgentExecution` / `AgentExecutionStep` / `AgentExecutionTrace`.

## Escena 9 — Manejo de errores (08:00-08:30)
- Comentar la `EstrategiaError` (Continuar/Reintentar/Cancelar) y que se registra.

## Escena 10 — Bloqueo de colaboración no autorizada (08:30-09:30)
- Crear regla `Comercial → RRHH = Denegado` (ya hecha en Escena 1).
- Enviar pregunta que intente involucrar RRHH desde Comercial → el Orchestrator lo excluye
  (Regla 3: deny by default).

## Cierre (09:30-10:00)
- Recap: un Orchestrator, agentes sin llamadas directas, contexto seguro, respuesta única, auditoría completa.
