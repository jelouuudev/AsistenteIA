# Arquitectura Final — Asistente Inteligente Empresarial (ETAPA 15)

## 1. Diagrama de capas
```
USUARIO
   │
   ▼
[ WEB APP - ASP.NET Core MVC + Bootstrap 5 ]   :5206
   │  (ApiService + JwtAuthorizationHandler)
   ▼
[ CHAT / API - ASP.NET Core 8 REST ]           :5298
   │
   ▼
[ AI ORCHESTRATOR - ChatService ]
   ├── RAG ───────► ChromaDB (:8000) + Embeddings (Ollama nomic-embed-text)
   ├── TOOLS ─────► SQL Server (SqlQueryTool, autorizado por fuente)
   └── WORKFLOW ──► EVENT ENGINE (Quartz BackgroundService)
   │
   ▼
[ OLLAMA - DeepSeek-R1-Distill-Qwen-7B ]       :11434
   │
   ▼
[ AUDITORÍA + LOGS/MÉTRICAS ]  (Serilog → Logs/, AuditoriaActividad, MetricasIA)
```

## 2. Capas del proyecto
- **Asistente.Domain**: entidades, interfaces, reglas de negocio.
- **Asistente.Shared**: DTOs compartidos Web↔API.
- **Asistente.Application**: servicios (Chat, Autorizacion, Permiso, PoliticaIA, ProteccionDatos, PromptInjection, RateLimit, DashboardSeguridad), Tool Orchestrator, Workflows.
- **Asistente.Infrastructure**: EF Core (DbContext, migraciones), repositorios, Quartz, BackgroundServices (Scheduler/Eventos/Indexación), ChromaVectorStore, Ollama clients, DependencyInjection.
- **Asistente.API**: controllers REST, middleware (Exception, RateLimiting), Health, JWT, Program.cs.
- **Asistente.Web**: MVC, Bootstrap, ApiService, Seguridad controllers/views.
- **Asistente.Tests**: xUnit + Moq (169 tests verdes).

## 3. Flujo de información (Prueba funcional 1)
Login → JWT → Web llama API `/api/chat/enviar` → ChatService verifica autorización (asistente/fuente/herramienta) → orquesta RAG/Tools/Workflow → Ollama genera → respuesta auditada y devuelta a Web.

## 4. Dependencias externas
- SQL Server (persistencia + SqlQueryTool).
- Ollama (generación + embeddings).
- ChromaDB (RAG).
- Quartz.NET (scheduler/eventos).
- Serilog (logs).

## 5. Seguridad y gobierno (ETAPA 14)
- Identity + JWT; roles Administrador/Supervisor/Usuario/Operador; 17 permisos por módulo.
- `AutorizacionService` (RBAC), `ProteccionDatosService` (enmascaramiento), `PromptInjectionService` (multilenguaje), `RateLimitService` (ventana 60s).
- Auditoría unificada: `AuditoriaActividad` (operaciones) + `AuditoriaIA` (uso de IA) + `MetricasIA`.

## 6. Contenedores (ETAPA 15)
- `Asistente.API/Dockerfile`, `Asistente.Web/Dockerfile`.
- `docker-compose.yml` con perfiles `dev`/`test`/`prod` (chromadb, mssql, ollama, api, web).
- `.env.example` (sin secretos). Configuración por ambiente vía `appsettings.Production.json` + variables de entorno.
