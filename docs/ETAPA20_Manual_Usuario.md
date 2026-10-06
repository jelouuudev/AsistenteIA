# ETAPA 20 — Manual de usuario: Conectores externos

Todo se administra desde `api/conectores` (se requiere sesión; Swagger disponible).

## 1. Registrar un conector REST (ej. CRM)

```http
POST /api/conectores
{
  "codigo": "CRM_DEMO",
  "nombre": "CRM demo",
  "tipo": "REST",
  "requierePermiso": true,
  "configuracion": { "BaseUrl": "https://crm.empresa.local/api" },
  "credencial": { "tipo": "ApiKey", "secreto": "la-clave" },
  "politica": { "timeoutSegundos": 30, "maxReintentos": 2, "rateLimitPorMinuto": 60,
                "circuitBreakerUmbralFallos": 5, "circuitBreakerSegundosAbierto": 60 }
}
```

Tipos soportados: `REST`, `SOAP` (config `Endpoint`), `SMTP` (config `Host`,
`Puerto`, `SSL`, `Remitente`; opcional `PickupDirectory` para pruebas sin servidor),
`Webhook` (config `Url`), `Microsoft365` (config `TokenUrl`; credencial `OAuth2`),
`SharePoint` (config `SiteUrl` + `TokenUrl`; credencial `OAuth2`).
Credenciales: `None`, `ApiKey` (+`ApiKeyHeader` o `ApiKeyQuery`), `Basic`
(`nombreUsuario` + secreto), `Bearer` (secreto), `OAuth2` (`nombreUsuario`=client_id
o parámetro `client_id`, secreto=client_secret, parámetros `scope`, config `TokenUrl`).

## 2. Probar la conexión

```http
POST /api/conectores/CRM_DEMO/probar
→ { "ok": true, "error": null }
```

## 3. Ejecutar una llamada

```http
POST /api/conectores/CRM_DEMO/ejecutar
{ "operacion": "Obtener cliente", "recurso": "clientes/1", "metodo": "GET" }
```

Para SMTP el `recurso` es el destinatario (`"a@empresa.local"`) y
`parametros.Asunto` el asunto. Para Webhook el `cuerpo` es el JSON del evento.

## 4. Deshabilitar / habilitar (sin recompilar)

```http
POST /api/conectores/CRM_DEMO/deshabilitar   → las llamadas se rechazan y se auditan
POST /api/conectores/CRM_DEMO/habilitar
```

## 5. Cambiar credenciales

```http
PUT /api/conectores/CRM_DEMO/credencial
{ "tipo": "ApiKey", "secreto": "nueva-clave" }
```

## 6. Dashboard y auditoría

- `GET /api/conectores/{id}/metricas` → total, exitosas, fallidas, latencia
  promedio/máxima, reintentos.
- `GET /api/conectores/{id}/auditoria?tope=100` → usuario, agente, operación,
  destino (sin secretos), estado, latencia, error, resumen.

## 7. Permisos

Si el conector tiene `requierePermiso: true`, cada usuario necesita el permiso
`CONNECTOR:{Codigo}` (se otorga en el módulo de Seguridad). Sin él, la llamada
se rechaza antes de salir y queda auditada como `SinPermiso`.

## 8. Uso desde el Planner

La herramienta `Conector via Gateway` (`GatewayConnectorTool`) expone los
conectores a los agentes con los parámetros `conector`, `operacion`, `recurso`,
`metodo` y `cuerpo`. Toda llamada queda auditada con el usuario y el asistente.
