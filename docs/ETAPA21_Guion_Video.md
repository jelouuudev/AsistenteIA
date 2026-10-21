# ETAPA 21 — Guion del video demostrativo (≈ 8 min)

## 0. Preparación (fuera de cámara)
- Stack levantado; sesión admin en Web y Swagger. ChromaDB arriba (búsqueda semántica real).

## 1. Crear una memoria organizacional (0:00–1:30)
- [Web → Memorias → Crear] `FORM-REPORTE-MENSUAL`, ámbito `Organizacion`,
  categoría `FormatoReporte`, contenido con el formato. Guardar → Detalle en v1.
- Di: *«Genera la versión 1 y la indexa para búsqueda semántica.»*

## 2. Consultar memoria desde un agente (1:30–3:00)
- [Swagger `GET /api/memorias/FORM-REPORTE-MENSUAL`] → 200 con el contenido.
- [Swagger misma URL sin el permiso (otro usuario o permiso retirado)] → **403**
  `Sin permiso MEMORIA:...`. Di: *«Sin permiso no hay contenido: ni siquiera el error lo expone.»*
- [Web → caja de búsqueda] `formato de reporte mensual` → aparece con puntaje.

## 3. Reutilizar contexto en un workflow (3:00–4:30)
- Mostrar dos guardados de estado (`paso=2`, `paso=3`) y la tabla de versiones
  creciendo. Di: *«Cada guardado es una versión nueva; el workflow retoma donde quedó.»*

## 4. Versionar una memoria (4:30–5:30)
- [Detalle → Nueva versión] cambiar el formato con motivo → v2/v3.
- [Revertir a v1] → nace v3/v4 con el contenido original. Di: *«Revertir no borra historia: agrega.»*

## 5. Aplicar retención (5:30–6:30)
- Crear `TEMP-CADUCA` con fecha de ayer → [Retención] la lista como pendiente →
  **Aplicar** → desactivada (o purgada según política) + evento en auditoría.

## 6. Dashboard y auditoría (6:30–7:30)
- [Memorias] tarjetas + filtro por ámbito + tabla con versión/permiso/vencimiento.
- [Detalle → Auditoría] leer una fila: operación, resultado, motivo, fecha.
  Mostrar un `Denegado` a propósito (pedir sin permiso).

## Notas de grabación
- La memoria restringida para el 403 es `SECRETA` (ya está en la base de datos,
  ámbito `Organizacion`, exige el permiso `MEMORIA:SECRETA`): **cualquier** usuario
  recibe 403 al pedirla, el administrador también. No hay que conceder nada.
- El formulario de creación **no** tiene casilla de "requiere permiso": quien
  decide el acceso es el ámbito de la memoria (Usuario, Agente y Workflow solo la
  ven su dueño; Organizacion es compartida). Dilo así si te preguntan por ella.
- Si ChromaDB está caído, la búsqueda degrada a texto (decirlo si pasa: es diseño).
