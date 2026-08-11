# Documento de Configuración — ETAPA 15

## 1. appsettings.json (desarrollo) — claves
- `ConnectionStrings:DefaultConnection` → SQL Server.
- `Ollama:Url` `:11434`, `Modelo` `qwen2.5:14b`, `Temperatura` 0.3, `MaxTokens` 8192.
- `Embedding:BaseVectorial` InMemory (dev) / Chroma (prod), `TopK` 20, `PuntajeMinimo` 0.15.
- `MotorConsultas:MaximoRegistros` 100, `MaxConsultasSimultaneas` 5.
- `JwtSettings:SecretKey` (dev demo), `Issuer` AsistenteIA, `Audience` AsistenteIA_Users.

## 2. appsettings.Production.json (producción)
- Serilog en Warning/Error (menos ruido).
- `Ollama:Modelo` `deepseek-r1:7b`, `Embedding:BaseVectorial` `Chroma`, `TopK` 5, `PuntajeMinimo` 0.2.
- `RateLimiting`: General 120/min, Herramienta 20/min, Sql 10/min, Workflow 10/min.
- `Seguridad`: EnmascararDatosSensibles=true, BloquearPromptInjection=true, AuditarTodasLasOperaciones=true.
- `Scheduler`: procesamiento/eventos/indexación activos.
- **Secretos vía variables de entorno** (Docker/`.env`), nunca en claro en el repo.

## 3. docker-compose.yml (perfiles)
- `dev`/`test`/`prod`. Servicios: chromadb, mssql, ollama, api, web.
- Variables desde `.env` (ver `.env.example`).

## 4. Límites y seguridad
- Rate limiting por ventana 60 s por `{usuario}:{ip}:{categoria}`.
- Máximo 100 filas en SQL, 5 consultas simultáneas.
- Autorización por rol/permiso en cada ruta y en el orquestador (asistente/fuente/herramienta/workflow).

## 5. Scheduler / Eventos
- Quartz 3.8.1; BackgroundServices: procesamiento documental (30 s), eventos, indexación automática.
