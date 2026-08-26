# Guion de Video — ETAPA 13: Motor de Eventos Empresariales y Automatización Proactiva

**Duración objetivo:** 10–15 minutos. **Formato:** demo en vivo (Web + API) con narración.

> Prerrequisitos de grabación: levantar `dotnet run --project Asistente.API` y
> `dotnet run --project Asistente.Web`; iniciar sesión como **admin/admin**.

---

## Escena 0 — Introducción (0:30)
- Mostrar el menú y la sección "Flujos / Monitoreo" resaltando los nuevos módulos:
  **Eventos**, **Tareas**, **Reglas de Automatización**, **Monitoreo Eventos**.
- Explicar el objetivo: el sistema reacciona solo ante eventos y ejecuta flujos sin
  intervención del usuario.

## Escena 1 — Registro de un evento (1:30)  [Caso 1]
- Ir a **Eventos → Nuevo Evento**.
- Crear `DOC_PROCESADO` — "Documento Procesado", Categoría *Documento*, activo.
- Guardar y mostrarlo en la lista con badge **Activo**.
- Comentar que el evento queda disponible para asociarlo a reglas.

## Escena 2 — Configuración de una regla (2:00)  [Caso 2]
- **Configuración → Reglas de Automatización → Nueva Regla**.
- Evento origen = `DOC_PROCESADO`, Flujo = **Reporte de Clientes**, Prioridad 1.
- Explicar la **Condición** (dejar vacía = siempre; ej. `Categoria == 'Error'`).
- Guardar y mostrar la regla en la lista como **Activa**.
- Explicar: "cuando ocurra el evento, el flujo se ejecutará automáticamente".

## Escena 3 — Ejecución automática de un workflow (2:30)  [Caso 2 + flujo]
- Desde **Eventos**, pulsar el botón ▶ **Disparar** sobre `DOC_PROCESADO`.
- Abrir **Monitoreo Eventos → Eventos Procesados**: mostrar el nuevo registro pasando de
  *Pendiente* → *Completado* con `TiempoProcesamiento` (ms) y `NombreWorkflow = Reporte de Clientes`.
- Recalcar: **sin intervención del usuario** el workflow se ejecutó solo (motor desacoplado +
  Workflow Engine).

## Escena 4 — Programación de una tarea (2:00)  [Caso 3]
- **Tareas → Nueva Tarea**: `Reporte de Clientes Periódico`, Cron `0 0/30 * * * ?`,
  Flujo = Reporte de Clientes, Activa.
- Guardar; mostrar en la lista con próxima ejecución.
- Explicar Quartz.NET y que la tarea se reprograma al reiniciar la API.
- (Opcional) Mostrar en el panel de monitoreo la `UltimaEjecucion`/`ProximaEjecucion` una vez
  que Quartz la dispara.

## Escena 5 — Manejo de errores y reintentos (2:30)  [Caso 4]
- Ir a **Monitoreo Eventos → Configuración** y mostrar *Reintentos máximos*, *Intervalo* y
  *Eventos simultáneos máximos*.
- Explicar la política: si el workflow falla, el motor reintenta; al agotar, marca el
  `EventoProcesado` como **Error** con el mensaje.
- Mostrar un ejemplo de error en **Eventos Procesados** (estado rojo) y cómo queda registrado
  para auditoría (incidente visible en el panel).
- (Demo rápida opcional) crear una regla que apunte a un flujo que falle para mostrar el
  reintento en vivo y el estado final *Error*.

## Escena 6 — Consulta del monitoreo (1:30)  [Caso 5]
- **Monitoreo Eventos**: tarjetas de resumen (eventos/activos, reglas/activas, exitosos/errores,
  tareas/activas), tablas de **Reglas activas**, **Tareas programadas** y **Últimos eventos
  procesados**.
- Abrir **Eventos Procesados** para evidenciar la **auditoría completa** (evento, regla, workflow,
  usuario, resultado, tiempo).

## Escena 7 — Cierre (0:30)
- Resumir cumplimiento del RF: eventos, reglas, ejecución automática, tareas programadas,
  reintentos, monitoreo y auditoría.
- Mencionar que el núcleo está listo para extenderse con colas distribuidas, webhooks e
  integración empresarial sin reescribir el motor.

---

**Total estimado:** ~13 minutos.
