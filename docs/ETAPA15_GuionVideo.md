# Guion de Video — Instalación y Puesta en Producción (ETAPA 15)

**Duración objetivo:** 15–20 min. **Herramientas:** OBS Studio o Xbox Game Bar (Win+G).

## Escena 1 — Introducción (1 min)
- Qué es el Asistente Inteligente Empresarial y la ETAPA 15 (pruebas, optimización, despliegue, documentación).

## Escena 2 — Requisitos (1 min)
- Hardware/software: .NET 8, SQL Server 2022, Ollama, Docker, ChromaDB.

## Escena 3 — Instalación de SQL Server + Ollama (2 min)
- Instalar SQL Server, crear BD `AsistenteIA`.
- `ollama pull deepseek-r1:7b`, `ollama pull nomic-embed-text`, verificar `:11434`.

## Escena 4 — ChromaDB + Docker (2 min)
- `docker compose --profile dev up chromadb` (o `docker run -p 8000:8000 chromadb/chroma:0.4.24`).
- Mostrar `:8000/api/v2/heartbeat` = OK.

## Escena 5 — Clonar y configurar (2 min)
- `git clone`, `dotnet restore`, copiar `.env.example`→`.env`, ajustar secretos (sin mostrarlos).

## Escena 6 — Migraciones y Seed (2 min)
- `dotnet ef database update`; arrancar API; mostrar logs de seed (roles, 17 permisos, admin).

## Escena 7 — Ejecutar en desarrollo (2 min)
- `dotnet run --project Asistente.API` y `Asistente.Web`; login admin; 1 chat de prueba.

## Escena 8 — Despliegue producción con Docker (3 min)
- `docker compose --profile prod up -d --build`; mostrar contenedores `asistenteapi`, `asistenteweb`, `asistentechroma`.
- `GET /api/health` = 200.

## Escena 9 — Seguridad y auditoría (2 min)
- Menú Configuración → Seguridad: usuarios, roles, permisos, asignar asistentes/fuentes.
- Mostrar bloqueo operador 403 en dashboard.

## Escena 10 — Backups (1 min)
- Ejecutar `scripts/backup_full.sql` y `backup_documents_config.ps1`; mostrar archivos en `C:\Backups`.

## Escena 11 — Monitoreo y cierre (1 min)
- Logs Serilog en `Logs/`, health checks, alertas (ver `ETAPA15_MonitoreoAlertas.md`).
- Cierre: sistema listo para producción.

## Tips
- No mostrar contraseñas/JWT reales en pantalla.
- Usar zoom en zonas de configuración.
- Hablar en español (proyecto universitario).
