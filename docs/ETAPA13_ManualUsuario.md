# Manual de Usuario — ETAPA 13: Motor de Eventos Empresariales y Automatización Proactiva

## 1. Introducción

El **Motor de Eventos Empresariales** permite que el sistema reaccione automáticamente ante
eventos del negocio (por ejemplo, "Documento Procesado") ejecutando flujos de trabajo
preconfigurados, sin que un operador tenga que dispararlos manualmente. También permite
programar tareas periódicas (reportes diarios, reindexación, etc.) y monitorear todo lo
ejecutado.

Acceda con un usuario **Administrador** (el monitoreo también está disponible para
**Supervisor**).

## 2. Módulos en el menú

| Opción | Qué hace |
|---|---|
| **Eventos** (menú Flujos) | Catálogo de eventos empresariales: crear, activar/desactivar, eliminar y disparar (prueba). |
| **Reglas de Automatización** (menú Configuración) | Asocia un evento a un workflow con prioridad y condición. |
| **Tareas** (menú Flujos) | Programa tareas periódicas con expresión Cron. |
| **Monitoreo Eventos** (menú Monitoreo) | Panel con eventos, reglas, tareas y ejecuciones; auditoría; configuración del motor. |

## 3. Caso 1 — Registrar un evento

1. Ir a **Eventos → Nuevo Evento**.
2. Complete **Código** (único, sin espacios, p.ej. `DOC_PROCESADO`), **Nombre**,
   **Descripción** y **Categoría** (Sistema/Documento/Seguridad/Negocio/Programado).
3. Deje **Evento activo** marcado.
4. Guardar. El evento queda disponible para asociarlo a reglas.

## 4. Caso 2 — Configurar una regla

1. Ir a **Configuración → Reglas de Automatización → Nueva Regla**.
2. Seleccione **Evento origen** y **Flujo de trabajo asociado**.
3. (Opcional) Escriba una **Condición** (p.ej. `Categoria == 'Error'`). Déjela vacía para
   ejecutar siempre.
4. Establezca **Prioridad** (mayor = se evalúa primero).
5. Guardar. Ahora, cuando ocurra el evento, el flujo se ejecutará automáticamente.

## 5. Caso 3 — Disparar un evento (prueba)

En **Eventos**, use el botón ▶ de un evento para dispararlo manualmente. El sistema crea el
registro de auditoría y el procesador en segundo plano ejecuta el workflow asociado. Revise el
resultado en **Monitoreo Eventos**.

## 6. Caso 4 — Programar una tarea diaria

1. Ir a **Tareas → Nueva Tarea**.
2. Nombre, **Flujo de trabajo** y **Expresión Cron** (p.ej. `0 0 2 * * ?` = cada día a las 2 AM).
3. Guardar. La tarea se reprogramará al reiniciar la API y se ejecutará conforme a su horario.

> Las tareas se reprograman al reiniciar la API. Para cambiar el horario, edite la expresión
> Cron y reinicie el servicio (o recree la tarea).

## 7. Caso 5 — Consultar el monitoreo

Ir a **Monitoreo Eventos** para ver:
- Tarjetas resumen: eventos/activos, reglas/activas, exitosos/errores, tareas/activas.
- **Reglas activas** y **Tareas programadas** (con última y próxima ejecución).
- **Últimos eventos procesados** (evento, fecha, estado, tiempo en ms).

Desde **Monitoreo Eventos → Eventos Procesados** se ve la **auditoría completa** (evento,
regla, workflow, usuario, resultado y tiempo de cada ejecución).

## 8. Configuración del motor

En **Monitoreo Eventos → Configuración** puede ajustar:
- **Reintentos máximos** y **Intervalo entre reintentos (ms)**.
- **Tiempo máximo por evento (ms)**.
- **Eventos simultáneos máximos**.
- **Frecuencia del procesador (ms)**.

Esto controla la política de reintentos y la concurrencia del procesador desacoplado.

## 9. Manejo de errores

Si un workflow falla al ejecutarse automáticamente, el motor aplica los reintentos configurados
y, al agotarlos, marca el `EventoProcesado` como **Error** con el mensaje correspondiente.
Dicho incidente es visible inmediatamente en el panel de monitoreo (sección de errores).
