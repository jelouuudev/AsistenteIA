# Manual Técnico — Asistente Inteligente Empresarial (ETAPA 15)

## A. Arquitectura (resumen)
Ver `ETAPA15_ArquitecturaFinal.md`. Capas: Domain, Shared, Application, Infrastructure, API, Web, Tests.

## B. Base de datos (SQL Server)
- Contexto: `Asistente.Infrastructure/Data/AsistenteDbContext`.
- Migraciones: `Asistente.Infrastructure/Migrations/*` (incluye `Etapa14_SeguridadGobernanza`).
- Tablas principales: `Usuario`, `Rol`, `Permisos`, `RolPermisos`, `UsuarioAsistentes`, `UsuarioFuentes`, `Asistente`, `FuenteConocimiento`, `Documento`, `ChunkDocumento`, `Herramientas`, `ConfiguracionWorkflow`, `Workflows`, `EventosEmpresariales`, `ConfiguracionEventoMotor`, `AuditoriaActividad`, `AuditoriaIA`, `MetricasIA`, `PoliticasIA`.
- Índices: claves foráneas con índices en `UsuarioAsistentes(IdUsuario,IdAsistente)`, `UsuarioFuentes(IdUsuario,IdFuente)`, `AuditoriaActividad(Fecha)`, `ChunkDocumento(IdDocumento)`.
- Seed: `DbInitializer.SeedAsync` (roles, 17 permisos, políticas IA, usuarios admin/operador/supervisor/usuario).

## C. Inteligencia Artificial
- Modelo: DeepSeek-R1-Distill-Qwen-7B (prod) / qwen2.5:14b (dev) vía Ollama `:11434`.
- Generación: `ChatService` → `OllamaClient` (HttpClient). Temperatura 0.3, MaxTokens 8192.
- Embeddings: `OllamaEmbeddingProvider` (nomic-embed-text).

## D. RAG
- `ChromaVectorStore` (HttpClient a ChromaDB `:8000`) o InMemory (dev).
- Chunking: 1000 chars, overlap 200 (`ProcesamientoDocumental`).
- Recuperación: TopK 5 (prod) / 20 (dev), threshold 0.20 (prod) / 0.15 (dev).
- Indexación automática: BackgroundService cada 30 s (procesa hasta 5 docs/ciclo).

## E. Tools
- `ToolOrchestrator` ejecuta herramientas según autorización (`AutorizacionService.VerificarHerramientaAsync`).
- `SqlQueryTool`: consultas parametrizadas, solo SELECT, máx 100 filas, requiere `SQL_CONSULTAR`.

## F. Workflows
- Definidos en `ConfiguracionWorkflow` / `Workflows`; orquestados por el motor, disparados por eventos o scheduler.

## G. Eventos
- Eventos internos: `EventoProcesado`, `ConfiguracionEventoMotor`, `EventosEmpresariales`.
- Eventos programados: Quartz BackgroundService (`ProcesadorEventosBackgroundService`).
- Ejemplo: `DocumentoCargado` → Workflow de indexación/embeddings.

## H. Seguridad (ETAPA 14)
- Identity + JWT (`JwtSettings`, issuer `AsistenteIA`, audience `AsistenteIA_Users`).
- `AutorizacionService` (RBAC), `PermisoService` (17 permisos), `PoliticaIAService`, `ProteccionDatosService`, `PromptInjectionService`, `RateLimitService` (ventana 60s).
- Middleware: `ExceptionMiddleware`, `RateLimitingMiddleware`.

## I. Logging y monitoreo
- Serilog → Console + File (`Logs/log-.txt`, rolling diario, 30 días).
- Health: `GET /api/health` (DB+Ollama), `GET /health` (Web).
- Métricas: `MetricasIA`, `DashboardSeguridadService`.

## J. Despliegue
- Docker: `Asistente.API/Dockerfile`, `Asistente.Web/Dockerfile`, `docker-compose.yml` (perfiles dev/test/prod).
- Producción: `appsettings.Production.json` + variables de entorno (`.env`, no commiteado).
- Backups: `scripts/backup_full.sql`, `backup_diff.sql`, `backup_documents_config.ps1`, `restore_sql.sql`.

## K. Testing
- xUnit + Moq en `Asistente.Tests`. Suite: 169 tests verdes (2 pre-existentes no ETAPA 15).
- `SeguridadEtapa14Tests` 10/10. Ver reportes en `docs/ETAPA15_reporte_*.md`.
