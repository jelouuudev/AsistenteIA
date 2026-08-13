# Reporte de Pruebas de Rendimiento y Concurrencia — ETAPA 15

**Fecha:** 2026-08-11 (desarrollo) · **2026-08-13 (stack Docker Opción A)**.
**Entorno A (desarrollo):** laptop Windows 11, 1 CPU lógica, SQL Server local, Ollama qwen2.5:14b en CPU.
**Entorno B (Docker Opción A):** docker compose --profile prod (api+web+chromadb+ollama+mssql contenedores), Ollama deepseek-r1:7b en **CPU** del host Windows.

**Nota GPU:** Las pruebas de carga con generación IA (DeepSeek/Ollama) NO se ejecutaron a N=50 aquí porque cada respuesta tarda ~90–150 s en CPU y el rate-limiter (`RateLimiting.General.Maximos=120`/60 s) bloquea la concurrencia alta. La métrica de generación IA debe medirse en el servidor de producción con GPU.

## Entorno B — Stack Docker (Opción A), Ollama en CPU

### Endpoints rápidos (sin Ollama)
Se simularon N usuarios concurrentes. Resultados:

| Endpoint | N | Exitosas | Errores | Nota |
|----------|---|----------|---------|------|
| `/api/health` | 5 | 5 | 0 | OK |
| `/api/health` | 10 | 10 | 0 | OK |
| `/api/health` | 25 | 25 | 0 | OK |
| `/api/health` | 50 | 19 | 31 | rate-limit General (120/60s) |
| `/api/seguridad/permisos` (auth) | 5 | 5 | 0 | <100 ms |
| `/api/seguridad/permisos` (auth) | 10 | 10 | 0 | <100 ms |
| `/api/seguridad/permisos` (auth) | 25 | 25 | 0 | <100 ms |
| `/api/seguridad/permisos` (auth) | 50 | 20 | 30 | rate-limit General (120/60s) |

### Chat RAG (Ollama deepseek-r1:7b en CPU)
| Métrica | Valor |
|---------|-------|
| `TiempoRespuestaMs` (generación IA) | **95 832 ms** (~96 s) en CPU |
| `TiempoConstruccionContextoMs` (RAG recovery, Chroma local) | **5 ms** ✅ |
| `exitoso` | true |
| Wall-clock total (N=1) | ~150 s |
| N=3 | no ejecutado (CPU se satura + rate-limit) |

### Interpretación
- A **N≤25** no hay errores de lógica: autorización y health manejan concurrencia con latencia sub-100 ms.
- A **N=50** aparecen ~30 errores por **rate-limiting de la aplicación** (`RateLimiting.General.Maximos=120` solicitudes / 60 s), NO por fallo de código ni de sockets. En producción con GPU y `HttpClient` reutilizado, el throughput de generación mejora y el límite se ajusta según capacidad.
- **RAG recovery = 5 ms** confirma que ChromaDB local (container) recupera contexto en tiempo óptimo (< 2 s objetivo). B4 verificado.

## Entorno A — Desarrollo (laptop, previo)
(Ver sección original abajo. Endpoints rápidos idénticos en perfil; aquí se midió línea base web+auth.)

## Métricas base recomendadas (a completar en producción con GPU)
| Métrica | Objetivo | Medido aquí (CPU) |
|---------|----------|-------------------|
| Tiempo promedio de respuesta (IA) | < 15 s (GPU) | 96 s (CPU, deepseek-r1:7b) |
| Tiempo máximo de respuesta | < 30 s | — (requiere GPU) |
| Tiempo de recuperación RAG | < 2 s | **5 ms** ✅ |
| Tiempo de consulta SQL | < 1 s | pendiente GPU |
| Tiempo de generación del modelo | medir en GPU | 96 s (CPU) |
| Tiempo de ejecución de Tools | < 5 s | pendiente GPU |
| Tiempo de ejecución de Workflows | < 30 s | pendiente GPU |

## Conclusión
**Pruebas de concurrencia EJECUTADAS** (datos reales capturados en stack Docker). El servidor maneja autorización concurrente con latencia sub-100 ms y 0 errores de lógica hasta 25 usuarios simultáneos. RAG recovery via Chroma local = 5 ms. Generación IA en CPU = ~96 s (deepseek-r1:7b); requiere GPU para cumplir <15 s. Ver `ETAPA15_reporte_concurrencia.json` para el detalle crudo.
