# ETAPA 21 — Manual de usuario: Memoria Empresarial

Todo en el Web (**Memorias**) o en `api/memorias` (con sesión). Los permisos los otorga Seguridad.

## 1. Crear una memoria organizacional (Caso 1: formato de reporte)

Web → Memorias → Crear: código `FORM-REPORTE-MENSUAL`, ámbito `Organizacion`,
categoría `FormatoReporte`, contenido con el formato. Genera la **v1** y la indexa
para búsqueda semántica. Si exige permiso, cada lector necesita `MEMORIA:FORM-REPORTE-MENSUAL`.

## 2. Consultar memoria desde un agente

- **Por UI/API**: `GET /api/memorias/{codigo}` (respeta permisos; 403 sin ellos).
- **Búsqueda semántica**: caja de búsqueda del Index o `GET /api/memorias/buscar?texto=...`.
  Solo devuelve memorias autorizadas y no expiradas.
- **Desde planes**: herramienta `Memoria institucional` (`MemoryTool`) con
  `operacion` = `buscar` (parámetro `texto`), `leer` (`codigo`) o `guardar`
  (`codigo`+`contenido`+`ambito`).

## 3. Reutilizar contexto en un workflow (Caso 3)

El workflow guarda su estado (`GuardarEstadoWorkflowAsync`: crea o versiona) y lo
retoma después (`LeerEstadoWorkflowAsync`). Cada guardado es una versión nueva
con motivo; el historial muestra la evolución paso a paso.

## 4. Versionar una memoria

Detalle → *Nueva versión* (contenido + motivo) → v2, v3... La historia es
inmutable: **Revertir a vN** crea una versión nueva copiando la elegida, sin
borrar nada. La tabla *Versiones* muestra cada contenido con fecha y motivo.

## 5. Aplicar retención

Web → Memorias → **Retención**: lista lo vencido (fecha explícita o días de la
política) y **Aplicar retención** desactiva o purga según política, dejando
evento con el código. Las purgadas desaparecen del índice y de listados; su
evento de purga conserva la trazabilidad.

## 6. Dashboard y auditoría

Index: tarjetas (activas, versiones, accesos, expiradas pendientes), filtro por
ámbito, búsqueda semántica y tabla con versión/permiso/vencimiento. Detalle:
contenido actual, formulario de nueva versión, tabla de versiones con revertir,
y tabla de auditoría (operación, resultado, motivo, fecha).
