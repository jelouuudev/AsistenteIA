# ETAPA 21 — Guion del video demostrativo (≈ 8 min)

## 0. Preparación (fuera de cámara)
- Stack levantado; sesión admin en Web y Swagger. ChromaDB arriba (búsqueda semántica real).
- Las **siete memorias de ejemplo ya están sembradas** (las crea `DbInitializer` al
  arrancar): `FORM-REPORTE-MENSUAL`, `DEF-VIATICOS`, `PROC-REPORTE-COMERCIAL`,
  `PREF-TONO-DIRECTO`, `CONOCIMIENTO-ASISTENTE-GENERAL`, `ESTADO-REPORTE-CLIENTES`
  y `SECRETA`. No hay que crear nada para el guion: se puede empezar directamente.

## 1. Crear una memoria organizacional (0:00–1:30)
- [Web → Memorias] mostrar `FORM-REPORTE-MENSUAL` ya sembrada (ámbito `Organizacion`,
  categoría `FormatoReporte`) y abrir su Detalle.
- [Detalle → **Indexar para búsqueda**] → queda indexada. Di: *«La indexo para que
  la búsqueda semántica la encuentre.»*
- Si se quiere crear una de cero (para el paso 4): [Web → Memorias → Crear] con otro
  código (p. ej. `FORM-REPORTE-COMERCIAL`), ámbito `Organizacion`, categoría
  `FormatoReporte`, contenido con el formato. Guardar → Detalle en v1.

## 2. Consultar memoria desde un agente (1:30–3:00)
- [Swagger `GET /api/memorias/FORM-REPORTE-MENSUAL`] → 200 con el contenido.
- [Swagger `GET /api/memorias/SECRETA`] → **403** `Sin permiso MEMORIA:SECRETA`.
  Di: *«Sin permiso no hay contenido: ni siquiera el error lo expone.»*
- [Web → caja de búsqueda] `formato de reporte mensual` → aparece con puntaje.
- [Chat] preguntar al Asistente General *«Usando la memoria institucional, indica el
  formato del reporte mensual y sus secciones»* → la respuesta llega con el bloque
  `[Memoria Organizacion: Formato de reporte mensual]`. Di: *«El agente no inventa el
  formato: lo recupera de la memoria de la organización.»*

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
- La memoria restringida para el 403 es `SECRETA` (viene sembrada, ámbito
  `Organizacion`, exige el permiso `MEMORIA:SECRETA`): **cualquier** usuario recibe
  403 al pedirla, el administrador también. No hay que conceder nada.
- El formulario de creación **no** tiene casilla de "requiere permiso": quien
  decide el acceso es el ámbito de la memoria (Usuario, Agente y Workflow solo la
  ven su dueño; Organizacion es compartida). Dilo así si te preguntan por ella.
- Si ChromaDB está caído, la búsqueda degrada a texto (decirlo si pasa: es diseño).
