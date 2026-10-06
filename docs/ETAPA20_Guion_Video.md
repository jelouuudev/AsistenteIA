# ETAPA 20 — Guion del video demostrativo (≈ 8 min)

## 0. Preparación (fuera de cámara)
- API y Web levantados; sesión de admin iniciada en Swagger (`/swagger`).
- Los conectores demo ya existen o se crean en cámara (toma 30 s cada uno).

## 1. Registrar un conector (0:00–1:30)
- `POST /api/conectores` → `API_DEMO` (REST, `BaseUrl: http://localhost:5298`).
- Mostrar la respuesta: `tieneCredencial`, `politica`, **sin ningún secreto**.
- `POST /api/conectores` → `SMTP_DEMO` (SMTP con `PickupDirectory`).

## 2. Probar conexión (1:30–2:30)
- `POST /api/conectores/API_DEMO/probar` → `{ ok: true }`.
- `POST /api/conectores/SMTP_DEMO/probar` → `{ ok: true }`.
- Comentar: la prueba no envía nada, solo valida configuración + alcance.

## 3. Consumir API REST (2:30–3:30)
- `POST /api/conectores/API_DEMO/ejecutar` (`recurso: api/health`) → `200`
  con el JSON de salud. Mostrar `latenciaMs` y `reintentos`.

## 4. Enviar correo (3:30–4:30)
- `POST /api/conectores/SMTP_DEMO/ejecutar` (destinatario + asunto + cuerpo)
  → `exitoso: true, codigo: 250`. Mostrar el `.eml` generado.

## 5. Ejecutar desde un agente (4:30–5:30)
- La herramienta `Conector via Gateway` (`GatewayConnectorTool`) está registrada
  y visible en `GET /api/herramientas` (id 1002, categoría Integracion).
- Explicar el flujo: paso Tool del Planner → Tool → Gateway → conector.

## 6. Dashboard (5:30–6:30)
- `GET /api/conectores/{id}/metricas` → total, exitosas, latencia promedio/máxima.

## 7. Auditoría (6:30–7:15)
- `GET /api/conectores/{id}/auditoria` → usuario, operación, destino **sin
  secretos**, estado, latencia, error. Mostrar el evento `Deshabilitado`
  como prueba de que incluso los rechazos se auditan.

## 8. Manejo de errores + deshabilitar (7:15–8:00)
- `POST /api/conectores/API_DEMO/deshabilitar` → ejecutar → error
  "está deshabilitado" + evento en auditoría. `habilitar` para revertir.
- Cierre: credenciales cifradas (mostrar la tabla con `ValorCifrado`),
  permisos `CONNECTOR:{Codigo}`, políticas sin recompilar.

## Notas de grabación
- SharePoint/Microsoft 365 y SOAP se demuestran con sus pruebas automatizadas
  (requieren tenant/servidor real); el framework es el mismo.
- Si una llamada tarda, es el timeout/retry de la política actuando: coméntalo.
