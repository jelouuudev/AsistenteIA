# Inventario Final de Componentes — ETAPA 15 (Actividad 6)

| Componente | Versión | Dependencias | Ubicación | Responsable |
|------------|---------|--------------|-----------|-------------|
| Aplicación Web (MVC) | ASP.NET Core 8 | Bootstrap 5, Asistente.API | `Asistente.Web/` | Equipo IA |
| API (REST) | ASP.NET Core 8 | EF Core 8, Serilog, Quartz 3.8.1 | `Asistente.API/` | Equipo IA |
| SQL Server | 2022 (Developer) | EF Core | `localhost\SQL` | DBA |
| Ollama | latest | modelo DeepSeek-R1-Distill-Qwen-7B / qwen2.5:14b | `:11434` | Infra |
| Modelo IA | deepseek-r1:7b | Ollama | Ollama | Infra |
| ChromaDB | 0.4.24 | Docker | `:8000` | Infra |
| RAG | EF + Chroma | Embeddings nomic-embed-text | `Asistente.Application` | Equipo IA |
| Tool Orchestrator | — | MediatR/DI | `Asistente.Application/Services` | Equipo IA |
| Workflow Engine | — | Quartz → Workflow | `Asistente.Application/Workflows` | Equipo IA |
| Event Engine | — | Quartz BackgroundService | `Asistente.Infrastructure/Services` | Equipo IA |
| Scheduler (Quartz) | 3.8.1 | — | `Asistente.Infrastructure` | Equipo IA |
| Auth (Identity+JWT) | ASP.NET Core 8 | `AutorizacionService` | `Asistente.Application/Services/Seguridad` | Equipo IA |
| Auditoría | EF Core | `AuditoriaActividad`, `AuditoriaIA` | `Asistente.Domain/Entities` | Equipo IA |
| Logging (Serilog) | 4.x | Sinks Console/File | `appsettings.json` | Infra |
| Monitoreo | Health checks | `/api/health`, `/health` | `HealthController`, Web | Infra |
| Backups | sqlcmd/PS | `scripts/` | `scripts/` | DBA/Infra |

## Configuración destacada
- Connection string: `Server=localhost;Database=AsistenteIA;Trusted_Connection=True;TrustServerCertificate=True;`
- Ollama URL: `http://localhost:11434`, Modelo: `qwen2.5:14b` (dev) / `deepseek-r1:7b` (prod).
- ChromaDB: `http://localhost:8000`.
- Puertos: API 5298, Web 5206, Ollama 11434, Chroma 8000.
- JWT issuer `AsistenteIA`, audience `AsistenteIA_Users`.
