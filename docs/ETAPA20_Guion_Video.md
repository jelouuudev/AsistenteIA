# ETAPA 20 — Guion del video demostrativo (≈ 10 min)

Todo lo de abajo está verificado en vivo. Entre corchetes, qué mostrar en pantalla.

## 1. Registrar un conector (0:00–1:00)
- [Swagger `POST /api/conectores`] Registrar `API_DEMO` (REST + `RecursoDefecto`).
- Mostrar la respuesta: `tieneCredencial: true` pero **ningún secreto**.
- [Web → Conectores → Registrar] Mostrar el formulario (mismo registro por UI).

## 2. Probar conexión (1:00–2:00)
- [Swagger `POST /api/conectores/API_DEMO/probar`] → `{ ok: true }`.
- [Web → Detalle → Probar conexión] Mismo resultado en el Dashboard.
- Comentar: valida alcance sin consumir presupuesto; queda auditado (`ProbarConexion`).

## 3. Consumir API REST (2:00–3:00)
- [Swagger ejecutar `API_DEMO`, recurso `api/health`] → 200 con el JSON real.
- [HubSpot] `CRM_HUBSPOT` → contactos reales (Brian Halligan, Maria Johnson).

## 4. SOAP en vivo (3:00–3:45)
- [Swagger ejecutar `CALC_SOAP`, operación `Add`, cuerpo `<Add ...><intA>12</intA><intB>30</intB></Add>`]
  → 200 con `<AddResult>42</AddResult>`.

## 5. Consultar SharePoint (3:45–4:45)
- [Swagger ejecutar `SP_STUB`, recurso `web/lists`] → bibliotecas (200).
- [recurso `web/lists('d1')/items`] → documentos con título y URL (200).
- Decir en voz alta: **"SharePoint simulado local con forma REST real"**
  (OAuth2, `_api`, Bearer y auditoría son código real; el servidor es el stub
  `scripts/stub_sharepoint.py` porque no hay tenant).

## 6. Enviar correo (4:45–5:45)
- [Swagger ejecutar `SMTP_DEMO` con destinatario + `Adjuntos` al PDF del reporte]
  → `exitoso: true`. Mostrar el `.eml` con el PDF adjunto en base64.

## 7. Ejecutar desde un agente/plan (5:45–7:15)
- [Web o API] Generar el plan "obten los contactos del CRM_HUBSPOT, busca
  documentos en SP_STUB y envia el resumen por SMTP_DEMO".
- Mostrar el DAG: 3 pasos Gateway en capa 1 (paralelo) + entrega en capa 2,
  con la huella idéntica en simulación, `GrafoValidado` y `GrafoEjecutado`.
- Ejecutar → plan **#2013 6/6 Completado**: correo enviado, contactos reales,
  bibliotecas. Cada pata auditada con su conector.

## 8. Dashboard (7:15–8:15)
- [Web → Conectores] Tabla con estado y permiso por conector.
- [Detalle `API_DEMO`] Tarjetas: llamadas, exitosas, fallidas, latencia.
  Configuración visible **sin secretos**. Botones Probar/Deshabilitar.

## 9. Auditoría (8:15–9:00)
- [Detalle → tabla Auditoría] usuario, operación, destino **sin query strings**,
  estado, latencia, error. Mostrar un evento `Deshabilitado` y uno `SinPermiso`
  como prueba de que hasta los rechazos se auditan.

## 10. Manejo de errores + deshabilitar (9:00–10:00)
- [Deshabilitar `API_DEMO`] → ejecutar → error "está deshabilitado" + evento.
- [Habilitar] para revertir. Cierre: credenciales AES, permiso
  `CONNECTOR:{Codigo}`, políticas (timeout/retry/rate-limit/breaker) sin
  recompilar, secretos rotables con `PUT credencial`.

## Notas de grabación
- El plan del punto 7 necesita los conectores registrados y la Tool
  `GatewayConnectorTool` asignada al agente (ya está hecho en este entorno).
- Si el planner no emite algún paso Gateway, el objetivo debe nombrar el
  CÓDIGO del conector (`CRM_HUBSPOT`, `SP_STUB`, `SMTP_DEMO`): la mención lo
  garantiza por diseño.
- Microsoft 365 se demuestra igual que SharePoint (conector `M365_STUB`,
  recurso `users`).
