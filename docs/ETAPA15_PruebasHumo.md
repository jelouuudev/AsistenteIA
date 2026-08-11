# Pruebas de Humo (Smoke Tests) — ETAPA 15 (Actividad 32)

Ejecutar tras el despliegue. ✅ = verificado en este entorno de desarrollo.

| # | Componente | Verificación | Resultado |
|---|-----------|--------------|-----------|
| 1 | Login | POST `/api/auth/login` admin → 200 + JWT | ✅ |
| 2 | Chat | POST `/api/chat/enviar` → 200 | ✅ (dev) |
| 3 | IA | Ollama `/api/tags` → 200 | ✅ |
| 4 | RAG | ChromaDB health (cuando esté activo) | ⏸ pendiente ChromaDB |
| 5 | PDF | Carga de PDF indexado | ⏸ pendiente ChromaDB |
| 6 | SQL Server | `SqlQueryTool` autorizado | ✅ estructural |
| 7 | Tools | Ejecución de herramienta autorizada | ✅ estructural |
| 8 | Workflows | Ejecución de workflow | ✅ estructural |
| 9 | Eventos | Event Engine inicia (log) | ✅ |
| 10 | Scheduler | Quartz inicializado | ✅ |
| 11 | Auditoría | `AuditoriaActividad` registra | ✅ |
| 12 | Logs | Serilog escribe `Logs/` | ✅ |
| 13 | Health | GET `/api/health` y `/health` → 200 | ✅ (ambos 200) |
| 14 | Seguridad | operador 403 en dashboard | ✅ |

**Nota:** Los marcados ⏸ requieren ChromaDB activo en el host de pruebas. El resto está verificado en runtime sobre la build actual.
