# Manual Técnico — ETAPA 14: Seguridad, Gobierno, Auditoría y Observabilidad

## 1. Objetivo
Capa integral de seguridad, gobierno y observabilidad para la plataforma de IA local, garantizando
autenticación, autorización por roles/permisos, control de acceso a asistentes/fuentes/herramientas/workflows,
auditoría de operaciones e interacciones de IA, protección de datos sensibles, mitigación de prompt injection,
políticas de IA, rate limiting y monitoreo.

## 2. Stack
ASP.NET Core 8 (API + Web MVC), C#, Bootstrap 5, SQL Server, Entity Framework Core 8, Serilog, JWT (cuando aplica),
Quartz.NET (ya presente de ETAPA 13).

## 3. Modelo de datos (nuevas entidades)
- `Permiso` (IdPermiso, Codigo, Nombre, Descripcion, Modulo, Activo)
- `RolPermiso` (IdRol, IdPermiso)
- `UsuarioAsistente` (IdUsuario, IdAsistente, Activo) — control de asistentes por usuario
- `UsuarioFuente` (IdUsuario, IdFuente, Activo) — control de fuentes por usuario
- `PoliticaIA` (IdPolitica, Nombre, Descripcion, Tipo, Valor, Activa)
- `AuditoriaActividad` (ya existía; se amplió con `TipoOperacion` y `Resultado`)
- `AuditoriaIA` (IdAuditoriaIA, IdUsuario, IdConversacion, Pregunta, IdAsistente, ModeloIA, PromptUtilizado,
  HerramientasUtilizadas, FuentesConsultadas, Respuesta, TiempoRespuestaMs, Resultado, FechaHora)
- `MetricasIA` (IdMetrica, IdUsuario, IdAsistente, TiempoGeneracionMs, TiempoRagMs, TiempoSqlMs, TiempoHerramientasMs,
  TokensEstimados, DocumentosRecuperados, HerramientasEjecutadas, FechaHora)

Migración: `20260810162521_Etapa14_SeguridadGobernanza`.

## 4. Servicios (Capa Application)
- `PermisoService` — `TienePermisoAsync(usuario, codigo)`, `ObtenerCodigosPorUsuarioAsync`, `ObtenerTodosAsync`.
- `AutorizacionService` — `VerificarAsistenteAsync`, `VerificarFuenteAsync`, `VerificarHerramientaAsync`
  (bloquea si el usuario no tiene el permiso de la herramienta: Casos 1 y 3).
- `PoliticaIAService` — gestión y consulta de políticas de IA.
- `ProteccionDatosService` — `Enmascarar` / `ContieneSensible` (Actividad 10 / Regla 8).
- `PromptInjectionService` — `EsMalicioso` (multilingüe ES/EN) y `SanitizarContenidoRecuperado` (Actividad 11 / Reglas 3 y 5).
- `RateLimitService` — ventana deslizante por clave+categoría (Actividad 13 / Caso 5).
- `DashboardSeguridadService` — agregados para el panel (Actividad 14 / 15).

## 5. Integración en el flujo de chat (Capa Application — ChatService)
En `ProcesarMensajeAsync` se aplican, en orden (flujo de seguridad del RF §7):
1. Rate limiting por usuario/IP.
2. Autorización de asistente (`VerificarAsistenteAsync`).
3. Enmascaramiento de datos sensibles en la pregunta (`ProteccionDatosService`).
4. Bloqueo de instrucciones de manipulación en el mensaje del usuario (`PromptInjectionService`).
5. Recuperación RAG → el contexto se **sanitiza** contra prompt injection.
6. Ejecución de herramientas → `ToolOrchestrator` valida permiso por herramienta (`AutorizacionService`).
7. Tras la respuesta: se escribe `AuditoriaIA` + `MetricasIA` (Actividades 9 y 15).

## 6. Autorización de herramientas (ToolOrchestrator)
`ValidarPermisosAsync` ahora invoca `IAutorizacionService.VerificarHerramientaAsync`. Una herramienta asociada a un
permiso (p.ej. `SqlQueryTool`→`SQL_CONSULTAR`/`SQL_ADMINISTRAR`, `ReportTool`→`HERRAMIENTAS_ADMINISTRAR`) solo se
ejecuta si el usuario posee dicho permiso (Caso 1 y 3). La IA nunca puede elevar privilegios (Regla 3).

## 7. Middleware
- `ExceptionMiddleware` (ya existente): manejo centralizado; detalle técnico a Serilog, mensaje amigable al cliente (Actividad 16 / Caso 6).
- `RateLimitingMiddleware` (nuevo): 60 req/min General, 20 Herramienta, 10 SQL, 10 Workflow; devuelve 429 + `Retry-After`.

## 8. Seed (DbInitializer)
Roles: Administrador, Supervisor, Usuario, Operador. 17 permisos por módulo. Asignación por mínimo privilegio
(Operador solo lectura/consulta; Usuario añade ejecución de workflows/SQL; Supervisor añade auditoría; Administrador todo).
7 políticas de IA por defecto. Al admin se le asignan todos los asistentes y fuentes.

## 9. Endpoints API (ETAPA 14)
- `GET /api/seguridad/permisos`, `GET /api/seguridad/permisos/mios`
- `GET /api/seguridad/politicas`, `POST /api/seguridad/politicas`
- `GET /api/seguridad/dashboard` (requiere AUDITORIA_CONSULTAR)
- `GET/POST /api/seguridad/usuarios/{id}/asistentes` (requiere ASISTENTES_ADMINISTRAR)
- `GET/POST /api/seguridad/usuarios/{id}/fuentes` (requiere FUENTES_ADMINISTRAR)

## 10. Compilación y pruebas
- Solución compila sin errores en los 6 proyectos.
- `dotnet test` → 169 pasan; 2 fallos preexistentes NO relacionados con ETAPA 14
  (`DocumentoServiceTests.CrearAsync_Should_Throw_When_CodigoDuplicado`, `RecuperacionServiceIsolationTests.Asistente_Solo_Recupera_De_Fuentes_Autorizadas`).
- 10 tests nuevos en `SeguridadEtapa14Tests` (Casos 1–5, enmascaramiento, autorización).

## 11. Verificación en runtime
- Login admin → token JWT (200).
- `/api/seguridad/permisos` → 17 permisos; `/politicas` → 7; `/dashboard` → métricas.
- Usuario `operador` (sin AUDITORIA_CONSULTAR) → `/dashboard` devuelve **403**; `/permisos` devuelve **200**.
- Web `/Seguridad/Dashboard`, `/Permisos`, `/Politicas` → 200.
