# Documento de Arquitectura de Seguridad — ETAPA 14

## 1. Visión general
Se añade una capa transversal de seguridad, gobierno y observabilidad sobre la arquitectura limpia (Domain /
Application / Infrastructure / API / Web). Principios aplicados: Clean Architecture, SOLID, OWASP, mínimo privilegio,
programación asíncrona, inyección de dependencias, DTO, FluentValidation, Serilog, manejo centralizado de excepciones,
auditoría y rate limiting.

## 2. Flujo de seguridad (defensa en capas)
```
Usuario
  │
  ▼ Autenticación (JWT/cookies, sesión, expiración, bloqueo de inactivos)
  ▼ Autorización (roles → permisos por módulo)
  ▼ Asistente autorizado (UsuarioAsistente)
  ▼ Fuentes autorizadas (UsuarioFuente)
  ▼ Herramientas autorizadas (permiso por herramienta vía AutorizacionService)
  ▼ Workflow autorizado (permiso WORKFLOWS_*)
  ▼ Ejecución (Motor de Herramientas / Workflow Engine)
  ▼ Auditoría (AuditoriaActividad + AuditoriaIA) y Métricas (MetricasIA)
```
Cada capa valida de forma independiente (Regla 1–8).

## 3. Componentes
- **Autenticación**: `AuthController` + `JwtTokenService` (secret en `JwtSettings:SecretKey`).
- **Autorización**: `PermisoService` resuelve códigos de permiso desde `RolPermiso`; `AutorizacionService` aplica
  reglas por asistente/fuente/herramienta. Administrador tiene bypass explícito.
- **Control de acceso a IA**: tablas `UsuarioAsistente` / `UsuarioFuente`; verificación en `ChatService` y `ToolOrchestrator`.
- **Auditoría**: `AuditoriaActividad` (operaciones) y `AuditoriaIA` (reconstrucción de interacción: usuario, conversación,
  pregunta, asistente, modelo, prompt, herramientas, fuentes, respuesta, tiempo, resultado). No se almacenan datos sensibles.
- **Protección de datos**: `ProteccionDatosService` enmascara DNI/tarjeta/credenciales antes de llamar al modelo (Regla 8).
- **Prompt Injection**: `PromptInjectionService` detecta (ES/EN) instrucciones de manipulación y neutraliza el contexto RAG
  (separación sistema vs. contenido; la IA no puede otorgar permisos — Reglas 3 y 5).
- **Políticas de IA**: `PoliticaIA` + `PoliticaIAService` (modelo, contexto, RAG, herramientas, tiempos, ejecuciones, fuentes).
- **Rate Limiting**: `RateLimitService` (ventana deslizante) + `RateLimitingMiddleware` (límites por categoría, 429).
- **Observabilidad**: `DashboardSeguridadService` + `MetricasIA`; Serilog para logs estructurados.
- **Manejo de errores**: `ExceptionMiddleware` (mensaje amigable + detalle a Serilog).

## 4. Reglas de negocio mapeadas
- R1 Usuario inactivo bloqueado → `Usuario.Activo` validado en login.
- R2 Asistente autorizado → `AutorizacionService.VerificarAsistenteAsync`.
- R3 La IA no otorga permisos → `PromptInjectionService` neutraliza instrucciones; sin camino de auto-elevación.
- R4 Herramienta no autorizada bloqueada → `ToolOrchestrator` + `AutorizacionService`.
- R5 RAG no modifica políticas → contexto sanitizado; políticas leídas solo desde `PoliticaIA`.
- R6 Operaciones sensibles requieren autorización → permisos `SQL_ADMINISTRAR`, `WORKFLOWS_ADMINISTRAR`, etc.
- R7 Operaciones críticas auditadas → `AuditoriaActividad` en todos los flujos.
- R8 Credenciales nunca al modelo → `ProteccionDatosService.Enmascarar`.

## 5. Consideraciones
- El `RateLimitService` es en memoria (exigido por el RF); en multi-instancia se recomienda Redis.
- El secreto JWT debe rotarse vía `appsettings.json` (`JwtSettings:SecretKey`), con longitud ≥ 32 caracteres.
- Las políticas y permisos se administran por UI; los cambios quedan auditados.
