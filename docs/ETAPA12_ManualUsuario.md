# Manual de Usuario — ETAPA 12: Motor de Flujos de Trabajo (Workflow Engine)

Este manual explica cómo administrar y ejecutar flujos de trabajo (workflows) en el
**Asistente Inteligente Empresarial**. Un flujo es un proceso automatizado que encadena
varias herramientas (consultas SQL, búsqueda documental, reportes, calculadora, fecha/hora)
en pasos secuenciales.

## 1. Acceso

Inicie sesión como **Administrador** (o **Supervisor** para el monitoreo). En la barra
superior aparece el ítem **Flujos**; en el menú **Configuración** encontrará
**Config. Workflow Engine**; y en la sección de monitoreo, **Monitoreo Flujos**.

## 2. Crear un flujo (Actividad 2 y 3)

1. Vaya a **Flujos → Nuevo Flujo**.
2. Complete los **datos del flujo**:
   - **Nombre**: identificador legible (ej. "Reporte de Clientes").
   - **Código**: único, sin espacios (ej. `REP-CLI`).
   - **Descripción** y **Frases disparadoras**: palabras que el asistente reconocerá en
     el chat para lanzar este flujo (separadas por `;`, ej. `reporte de clientes;generar reporte`).
3. En **Pasos del flujo** agregue uno o más pasos con:
   - **Nombre**, **Herramienta** (SqlQueryTool, DocumentSearchTool, ReportTool, CalculatorTool, DateTimeTool).
   - **Parámetros (JSON)**: use `{{resultado}}` para insertar el resultado del paso anterior.
   - **Reintentos**, **Tiempo máx. (ms)** y **Estrategia de error** (Cancelar / Omitir / RegistrarIncidencia).
   - **Requiere confirmación**: marque si el paso es sensible y debe pedir aprobación.
4. Guarde. El flujo queda en estado **Borrador**; actívelo desde la lista.

## 3. Activar / Suspender / Versionar (Actividad 2)

En la lista de **Flujos**:
- **▶ / ⏸** activa o suspende el flujo. Un flujo suspendido deja de estar disponible para los asistentes (Caso 5).
- **⎇ (Versión)** crea una copia del flujo (con sus pasos) en un **nuevo registro** con `Version + 1` y estado **Borrador**; el flujo original queda intacto. Aparecerá una fila nueva en la lista (p. ej. "Reporte de Clientes (v2)", código `REP-CLI-v2`). Así conservas el histórico y puedes editar la nueva versión sin perder la anterior.
- **🗑** elimina el flujo.

## 4. Configuración global (Actividad 10)

En **Configuración → Config. Workflow Engine** ajuste:
- **Reintentos máximos** (por paso), **Límite de pasos por workflow**.
- **Tiempo máximo por paso** y **Tiempo máximo del flujo** (ms).
- **Confirmaciones obligatorias**: si está activo, un paso marcado como sensible pedirá confirmación al usuario.

## 5. Ejecutar desde el chat (Actividad 9)

1. En el chat, escriba una frase que coincida con las *Frases disparadoras* del flujo
   (ej. *"generar reporte de clientes"*).
2. El asistente identifica el flujo y lo ejecuta paso a paso.
3. Si un paso **requiere confirmación**, el asistente pregunta *"Responde 'confirmar'
   para continuar"*. Responda **confirmar** (o *sí*) para reanudar y completar el flujo.
4. El asistente presenta el **resultado final**.

## 6. Monitoreo (Actividad 8)

En **Monitoreo Flujos** verá:
- Tarjetas resumen: **Total ejecuciones**, **Exitosas**, **Errores / Pendientes**, **Tiempo promedio**.
- Tabla de **últimas ejecuciones** con flujo, usuario, tiempo, **estado** (Exitosa / Pendiente conf. / Error) y resultado/error.

## 7. Ejemplo real (semilla del sistema)

El sistema incluye el flujo **"Reporte de Clientes"** (`REP-CLI`, Activo) con 3 pasos:
1. *Consultar clientes* (SqlQueryTool) → cuenta los clientes en la BD RestauranteDB.
2. *Generar reporte* (ReportTool) → usa `{{resultado}}` para armar el reporte (requiere confirmación).
3. *Presentar resultado* (DateTimeTool) → añade la fecha al reporte.

Prueba sugerida en el chat: *"generar reporte de clientes"* → confirme → reciba el reporte.
