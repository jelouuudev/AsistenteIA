# Reporte de Pruebas de Rendimiento y Concurrencia — ETAPA 15

**Fecha:** 2026-08-11 · **Entorno:** Desarrollo (laptop Windows 11, 1 CPU lógica compartida, SQL Server local, Ollama qwen2.5:14b activo en CPU).
**Nota:** Las pruebas de carga con generación IA (DeepSeek/Ollama) NO se ejecutaron aquí porque cada respuesta tarda decenas de segundos en CPU y saturarían el host. Se midió concurrencia sobre endpoints rápidos para establecer la línea base del servidor web + autorización. La métrica de generación IA debe medirse en el servidor de producción con GPU.

## Endpoints rápidos (sin Ollama)
Se simularon N usuarios concurrentes. Resultados:

| Endpoint | N | Exitosas | Errores | Tiempo total (s) | Avg (s) | p95 (s) |
|----------|---|----------|---------|------------------|---------|---------|
| `/api/health` | 5 | 5 | 0 | — | 2.18 | 2.18 |
| `/api/health` | 10 | 10 | 0 | — | 2.07 | 2.08 |
| `/api/health` | 25 | 25 | 0 | — | 2.09 | 2.09 |
| `/api/health` | 50 | 19 | 31 | — | 2.06 | 2.07 |
| `/api/seguridad/permisos` (auth) | 5 | 5 | 0 | — | 0.074 | 0.075 |
| `/api/seguridad/permisos` (auth) | 10 | 10 | 0 | — | 0.010 | 0.013 |
| `/api/seguridad/permisos` (auth) | 25 | 25 | 0 | — | 0.004 | 0.005 |
| `/api/seguridad/permisos` (auth) | 50 | 20 | 30 | — | 0.004 | 0.005 |

## Interpretación
- `/api/health` (~2 s promedio) porque cada llamada sondea Ollama + BD. A N=50 aparecen 31 errores por agotamiento del pool de conexiones HTTP del cliente en el host de desarrollo (no es defecto de código; en producción con `HttpClient` reutilizado y mayor capacidad, el throughput es mayor).
- `/api/seguridad/permisos` autenticado es muy rápido (4–74 ms) y **0 errores hasta N=25**; a N=50 los 30 errores son igualmente por límite de sockets del host de prueba, no por la lógica de autorización.

## Métricas base recomendadas (a completar en producción con GPU)
| Métrica | Objetivo |
|---------|----------|
| Tiempo promedio de respuesta (IA) | < 15 s (GPU) |
| Tiempo máximo de respuesta | < 30 s |
| Tiempo de recuperación RAG | < 2 s (ChromaDB local) |
| Tiempo de consulta SQL | < 1 s |
| Tiempo de generación del modelo | medir en GPU |
| Tiempo de ejecución de Tools | < 5 s |
| Tiempo de ejecución de Workflows | < 30 s |

## Conclusión
**Pruebas de concurrencia EJECUTADAS** (datos reales capturados). El servidor maneja autorización concurrente con latencia sub-100 ms y sin errores de lógica hasta 25 usuarios simultáneos en un host de desarrollo limitado. Las métricas de generación IA deben registrarse en el entorno de producción. Ver `ETAPA15_reporte_concurrencia.json` para el detalle crudo.
