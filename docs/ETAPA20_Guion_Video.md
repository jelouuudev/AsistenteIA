# ETAPA 20 — Guion del video demostrativo (≈ 12 min)

Todo verificado en vivo. Entre corchetes, qué mostrar. Frases honestas obligatorias marcadas con «di».

## 0. Preparación (fuera de cámara, 5 min)
- `docker compose --profile prod up -d` y esperar 1 min (el stub SharePoint/Graph arranca solo como servicio).
- Verificar: API `http://localhost:5298/swagger` (200), Web `http://localhost:5206`, conectores `API_DEMO`, `CRM_HUBSPOT`, `SP_STUB`, `M365_STUB`, `SMTP_DEMO`, `CALC_SOAP`, `WH_LIVE`, `HTTPBIN` en verde con Probar.
- Swagger logueado (admin) + Web logueado (admin) en dos pestañas.
- Grabar por escenas separadas, 1080p solo el navegador. Narrar cada clic ANTES de hacerlo; 2 s de silencio tras cada resultado.

## 1. Registrar un conector (0:00–1:30)
- [Web → Conectores → Registrar] Crear uno REST de ejemplo (código `DEMO_VIDEO`, `BaseUrl=https://httpbin.org`, `RecursoDefecto=get`).
- Mostrar en el Detalle que **no aparece ningún secreto** aunque se haya configurado credencial.
- Di: *«Los secretos se cifran con AES al guardar y jamás se devuelven: ni en pantalla, ni en API, ni en auditoría.»*

## 2. Probar conexión (1:30–2:30)
- [Detalle → Probar conexión] → *Conexión exitosa*. Mostrar el evento `ProbarConexion` en la auditoría.
- Di: *«Valida alcance sin gastar presupuesto de políticas.»*

## 3. Consumir API REST + verbos (2:30–4:00)
- [Plan] `prueba el verbo post usando el conector HTTPBIN` → ejecutar → 200 con eco del cuerpo.
- [Plan] `actualiza con el verbo put...`, `elimina con el verbo delete...` → 200 cada uno.
- Mostrar en la auditoría del conector `POST post`, `PUT put`, `DELETE delete`.

## 4. SOAP en vivo (4:00–5:00)
- [Plan] `suma con el conector CALC_SOAP` → `<AddResult>42</AddResult>` (12+30 por un servicio público real).
- Di: *«SOAP siempre sale por POST por protocolo; el conector ignora el campo método.»*

## 5. Consultar SharePoint (5:00–6:00)
- [Plan] `lista las bibliotecas usando el conector SP_STUB` → bibliotecas (200); luego `web/lists('d1')/items` → documentos con título y URL.
- **Di literalmente**: *«SharePoint simulado local con forma REST real: OAuth2, `_api`, Bearer y auditoría son código real; el servidor es un stub porque no hay tenant.»*

## 6. Microsoft 365 (6:00–6:45)
- [Plan] `lista los usuarios usando el conector M365_STUB` → Maria Johnson, Brian Halligan.
- Di: *«Mismo caso: Graph con forma real contra stub; contra un tenant solo cambian 4 cadenas.»*

## 7. Enviar correo + adjunto (6:45–7:45)
- [Plan] `envia un aviso usando el conector SMTP_DEMO` → `exitoso: true`.
- Mostrar el `.eml` con el PDF adjunto en base64. Si hay Gmail configurado, mostrar la bandeja real.

## 8. Webhook ida y vuelta (7:45–8:30)
- [Plan] `publica un evento usando el conector WH_LIVE` → 200.
- [Receptor] Mostrar la validación: firma buena → 200 `{"valido":true}`; firma mala → 401.
- Di: *«El envío lo ve cualquiera; la firma HMAC solo la verifica quien tiene el secreto.»*

## 9. Ejecutar desde un agente/plan multi-conector (8:30–10:00, la escena fuerte)
- Generar: `obten los contactos del CRM_HUBSPOT, busca documentos en SP_STUB y envia el resumen por SMTP_DEMO`.
- Mostrar el DAG en `/simular`: 3 pasos Gateway en capa 1 (paralelo) + entrega en capa 2, y leer la huella en voz alta.
- Ejecutar (cortar: tarda minutos) → 6/6: correo enviado, contactos reales de HubSpot, bibliotecas.
- Mostrar huellas idénticas en simulación, `GrafoValidado` y `GrafoEjecutado`.
- Si un paso es de escritura, mostrar la pausa de **aprobación** (se aprueba en Centro de Aprobaciones y sigue solo).

## 10. Dashboard (10:00–10:45)
- [Web → Conectores] Tabla con estado y permiso por conector.
- [Detalle `CRM_HUBSPOT`] Tarjetas (llamadas, exitosas, fallidas, latencia), configuración sin secretos, botones Probar/Editar/Habilitar.

## 11. Auditoría (10:45–11:30)
- [Detalle → tabla Auditoría] usuario, agente, operación, destino **sin query strings ni secretos**, estado, latencia, error. Mostrar un `SinPermiso` y un `Deshabilitado` como prueba de que hasta los rechazos se auditan.

## 12. Manejo de errores + deshabilitar + políticas (11:30–12:00)
- [Deshabilitar `API_DEMO`] → ejecutar → error + evento. [Habilitar] para revertir.
- Mencionar: timeout/retry/rate-limit/breaker configurables por conector; el breaker abrió en vivo tras fallos seguidos.
- Cierre a cámara: *«Gateway único, credenciales cifradas, permisos por conector, aprobación en escrituras, políticas sin recompilar y todo auditado.»*

## Notas de grabación
- Los objetivos deben **nombrar el CÓDIGO del conector** (`CRM_HUBSPOT`, no "el CRM"): la mención garantiza el paso.
- Los verbos se nombran igual (`verbo post`): si no, manda el defecto del conector.
- Si un plan mete un paso RAG no pedido con prosa irrelevante, es varianza del clasificador: regenera o sigue; no invalida la demo.
- `HTTPBIN`/`JSONPlaceholder`/dneonline/webhook.site son externos reales; M365/SharePoint van contra stub (decirlo); HubSpot y Gmail son cuentas reales.
