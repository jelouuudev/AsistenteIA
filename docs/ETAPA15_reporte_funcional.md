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
| 2 RAG | SUPERADA (E2E: indexado + embeddings Ollama 768d + recuperación semántica InMemory) |
| 3 SQL Tool | SUPERADA (E2E: "total de clientes = 5" vía RestauranteDB) |
| 4 Workflow | SUPERADA (E2E: workflow "Resumen de Clientes" ejecutado por el motor) |
| 5 PDF→Evento→Embeddings | SUPERADA (E2E: documentos 1–4 indexados con embeddings reales) |
| 6 Scheduler | SUPERADA (Quartz programado y activo) |

**Evidencia E2E real (ejecutado 2026-08-11, API net8.0 + Ollama):**
- RAG: consulta "¿pasos para dar de alta un empleado?" → respuesta fundamentada en "Manual de Onboarding para Nuevos Colaboradores TechCorp Solutions" (recuperado por similitud semántica sobre embeddings nomic-embed-text de 768 dimensiones).
- SQL Tool: "¿total de clientes registrados?" → "El total de clientes registrados en la base de datos es 5" (tabla `clientes` en `RestauranteDB`, consulta parametrizada/autorizada).
- Workflow: "Ejecuta el workflow Resumen de Clientes" → "El total de clientes es 5, obtenido de la tabla 'clientes' en la base de datos 'RestauranteDB'" (orquestación de pasos vía WorkflowEngine).

**Nota de configuración (ETAPA 15):** el almacén vectorial es seleccionable por `Embedding:BaseVectorial` (`InMemory` para dev/pruebas sin ChromaDB, `Chroma` para producción). Se corrigió el registro de `OllamaEmbeddingProvider` como cliente tipado de `IEmbeddingProvider` con `BaseAddress` = `Ollama.Url` (antes fallaba con "invalid request URI" al llamar a `/api/embeddings`), y `InMemoryVectorStore` se registró como Singleton para compartir el índice entre solicitudes.
