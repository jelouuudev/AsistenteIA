# Reporte de Pruebas Funcionales Integrales — ETAPA 15

**Fecha:** 2026-08-11 · **Entorno:** Desarrollo (Windows 11, SQL Server local, Ollama qwen2.5:14b, ChromaDB no disponible en host → degradación elegante de RAG).

## Prueba 1 — Login → Selección de asistente → Consulta → Respuesta
- Login admin (`/api/auth/login`) → **200 + JWT** ✓
- Consulta chat (`/api/chat/enviar`, IdAsistente=1) → **200**, respuesta generada por DeepSeek ✓
- **Resultado: SUPERADA**

## Prueba 2 — RAG sobre manual
- Carga de PDF y recuperación vía ChromaDB. **Pendiente de ejecución en host con ChromaDB** (el contenedor/servicio no está corriendo en este host). El código degrada correctamente (sin excepción). Ver `ETAPA15_reporte_optimizacion_rag.md`.

## Prueba 3 — Consulta SQL Server (SqlQueryTool)
- El flujo Tool → SQL Server → DeepSeek funciona (validado en etapas anteriores con autorización por usuario/fuente). **Requiere fuente SQL autorizada activa para prueba E2E.**

## Prueba 4 — Workflow empresarial
- Orquestador → Tools → Workflow → resultado. Validado estructuralmente; ejecución E2E pendiente de workflow de demo configurado.

## Prueba 5 — Carga de PDF → Evento DocumentoCargado → Embeddings → ChromaDB
- Event Engine y BackgroundService de procesamiento documental inician correctamente (logs: "Servicio de procesamiento documental iniciado"). RAG embebido pendiente de ChromaDB.

## Prueba 6 — Tarea programada (Scheduler/Quartz)
- Quartz 3.8.1 inicializado: `Quartz Scheduler 3.8.1.0 ... initialized`, RAMJobStore. Scheduler operativo ✓

## Conclusión
| Prueba | Estado |
|--------|--------|
| 1 Login→Chat | SUPERADA |
| 2 RAG | Parcial (requiere ChromaDB) |
| 3 SQL Tool | Parcial (requiere fuente autorizada) |
| 4 Workflow | Parcial (requiere workflow demo) |
| 5 PDF→Evento→Embeddings | Parcial (requiere ChromaDB) |
| 6 Scheduler | SUPERADA (Quartz OK) |

**Bloqueo:** ChromaDB y un workflow de demostración con fuente SQL deben estar activos en el host de pruebas para completar las Pruebas 2–5 E2E.
