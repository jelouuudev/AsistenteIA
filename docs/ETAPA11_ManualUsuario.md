# Manual de Usuario — ETAPA 11: Motor de Herramientas (Tool Orchestrator)

> Perfil sugerido: **Administrador** para gestión; **Operador** para usar el chat; **Supervisor**
> para el monitoreo.

## 1. ¿Qué es?
El Motor de Herramientas permite al asistente realizar acciones controladas (buscar en documentos,
consultar la base de datos, calcular, obtener la fecha/hora y generar reportes) de forma auditada y
con permisos. El usuario ve cuándo el asistente usó una herramienta.

## 2. Registrar y administrar herramientas
1. Menú **Herramientas** (o *Configuración → Herramientas (Orchestrator)*).
2. Lista con estado, categoría, permiso requerido, nº de ejecuciones, tiempo promedio y errores.
3. **Nueva Herramienta**: complete Nombre, Código (debe coincidir con la implementación),
   Descripción, Categoría, "Requiere permiso" y "Activa".
4. **Activar / Desactivar** desde los botones de la lista. Una herramienta desactivada deja de
   usarse de inmediato.

## 3. Asociar herramientas a un asistente
1. *Configuración → Asociar Herramientas*.
2. Elija un asistente y pulse **Gestionar herramientas**.
3. Pulse **Asociar** en cada herramienta que el asistente pueda usar (aparece como "Autorizada").
4. Para revocar, pulse la misma tarjeta ("Autorizada" → "Asociar").

> Ejemplo: un "Asistente Comercial" puede tener `DocumentSearchTool` + `SqlQueryTool` autorizadas,
> y dejar `ReportTool`/`UserProfileTool` sin autorizar.

## 4. Usar herramientas en el chat
1. Abra el **Asistente**, seleccione un asistente con herramientas asociadas.
2. Pregunte en lenguaje natural, p. ej.:
   - "Busca en el manual el procedimiento de reimbursos" → usa `DocumentSearchTool`.
   - "¿Cuántos clientes hay en total?" → usa `SqlQueryTool`.
   - "Calcula 125 por 8 entre 5" → usa `CalculatorTool`.
3. Bajo la respuesta aparece una tarjeta **Herramientas utilizadas** con el nombre y el tiempo.
   Si la herramienta falla, se muestra en rojo.

## 5. Monitoreo
- *Monitoreo Herramientas*: tarjetas con total de ejecuciones, exitosas, errores/rechazadas y
  tiempo promedio; tabla de auditoría (fecha/hora, herramienta, usuario, tiempo, estado, resultado).
- Cada fila es una ejecución registrada (caso de uso de auditoría obligatoria).

## 6. Configuración del orquestador
- *Configuración → Config. Orquestador*:
  - **Motor habilitado**: si se desactiva, ninguna herramienta se ejecuta.
  - **Prioridad**, **Tiempo máximo de ejecución (ms)**, **Máx. ejecuciones simultáneas**,
  - **Requiere autorización**: si está activo, las herramientas con "Requiere permiso" solo se
    ejecutan si están asociadas al asistente.

## 7. Casos de uso verificados
- ✅ Consulta documental → `DocumentSearchTool` usa el contenido recuperado.
- ✅ Conteo de clientes → `SqlQueryTool` incorpora los datos obtenidos.
- ✅ Operación matemática → `CalculatorTool` devuelve el resultado.
- ✅ Sin permisos / no asociada → ejecución rechazada y auditada.
- ✅ Desactivar herramienta → el asistente deja de usarla al instante.
- ✅ Panel de monitoreo → estadísticas y errores visibles.

## 8. Notas
- El asistente nunca accede directo a la base de datos; siempre pasa por el orquestador.
- `SqlQueryTool` solo lee (SELECT) sobre tablas/vistas autorizadas.
