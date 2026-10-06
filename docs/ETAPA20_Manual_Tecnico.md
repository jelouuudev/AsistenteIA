# ETAPA 20 — Manual técnico: API Gateway Empresarial y Conectores

## 1. Arquitectura

```
Agentes IA / Planner (Tool: GatewayConnectorTool)
  └─> ConnectorGateway (IConnectorGateway) — única puerta de salida
        ├─> Permisos: CONNECTOR:{Codigo} vía IAutorizacionService
        ├─> Políticas: Timeout / Retry / RateLimit / CircuitBreaker (ConnectorPolicyState, singleton)
        ├─> Auditoría: ConnectorExecution por cada llamada (destino sin secretos, respuesta resumida)
        └─> IConnector por Tipo:
              REST | SOAP | SMTP | Webhook | Microsoft365 (Graph + OAuth2) | SharePoint (REST + OAuth2)
```

Los agentes nunca consumen APIs externas directamente: el `GatewayConnectorTool`
recibe `{conector, operacion, recurso, metodo, cuerpo}` y delega en el Gateway.

## 2. Componentes (código)

| Pieza | Archivo |
|---|---|
| Entidades | `Asistente.Domain/Entities/Connector.cs` (Connector, Configuration, Credential, Policy, Execution) |
| Contratos | `Asistente.Domain/Interfaces/IConnector.cs` (IConnector, IConnectorGateway, ICredencialCifrador, ConnectorRequest/Result) |
| Repos | `Asistente.Application/Interfaces/IConnectorRepositories.cs`, `Asistente.Infrastructure/Repositories/ConnectorRepositories.cs` |
| EF | `Asistente.Infrastructure/Data/Configurations/ConnectorConfigurations.cs`, DbSets en `AsistenteDbContext`, migración `Etapa20_GatewayConectores` |
| Cifrado | `Asistente.Infrastructure/Services/CredencialCifrador.cs` (AES, clave `Gateway:ClaveCifrado`) |
| Gateway | `Asistente.Application/Services/Conectores/ConnectorGateway.cs` |
| Estado políticas | `.../Conectores/ConnectorPolicyState.cs` (ventana deslizante, circuito, caché OAuth2) |
| Admin | `.../Conectores/ConnectorService.cs` (el secreto se cifra al guardar; el DTO jamás lo devuelve) |
| Conectores | `HttpConnectorBase.cs`, `RestSoapWebhookConnectors.cs`, `SmtpMicrosoftSharePointConnectors.cs` |
| Tool | `Asistente.Application/Services/Herramientas/GatewayConnectorTool.cs` (Código `GatewayConnectorTool`) |
| API | `Asistente.API/Controllers/ConectoresController.cs` (`api/conectores`) |
| DI | `Asistente.Application/DependencyInjection.cs` + `Asistente.Infrastructure/DependencyInjection.cs` |
| SQL | `scripts/etapa20_gateway.sql` (idempotente) |
| Tests | `Asistente.Tests/Services/ConnectorGatewayTests.cs` (19 pruebas) |

## 3. Decisiones

- **Fail-closed**: conector inexistente, deshabilitado, sin permiso, sin implementación,
  rate-limit excedido o circuito abierto → resultado de error + evento de auditoría.
- **Secretos**: `ValorCifrado` (AES) + `ParametrosCifrados` (JSON cifrado). El `GET`
  devuelve solo `TipoCredencial`/`TieneCredencial`. La auditoría guarda el destino
  sin query string y la respuesta truncada a 2000 caracteres.
- **OAuth2**: client_credentials con caché en memoria (nunca se persiste el token).
- **SMTP sin servidor**: configuración `PickupDirectory` escribe `.eml` a disco
  (útil para pruebas y para la demo sin infraestructura de correo).
- **Timeout**: `CancellationToken` enlazado por intento; el retry respeta
  `MaxReintentos` + `IntervaloReintentoMs` y no reintenta cancelaciones del usuario.

## 4. Permisos

Cada conector con `RequierePermiso=true` exige `CONNECTOR:{Codigo}` (ver `scripts/etapa20_gateway.sql`
para otorgarlo). Sin el permiso, el Gateway rechaza antes de llamar fuera.

## 5. Pruebas

`dotnet test AsistenteIA.slnx` — 19 pruebas Etapa 20 con fakes (sin red):
cifrado roundtrip, resolución, permiso, retry, rate limit, circuit breaker,
timeout, ApiKey/OAuth2, SOAP, HMAC, SMTP pickup, SharePoint URL, DTO sin secreto,
Tool delega en Gateway.
