# Monitoreo y Alertas — ETAPA 15

## 1. Componentes monitoreados
| Componente | Señal | Herramienta sugerida |
|------------|-------|----------------------|
| Aplicación (API/Web) | `/health` HTTP 200 | Prometheus + Blackbox |
| SQL Server | conexión + CPU/RAM | sqlcmd + PerfMon |
| Ollama | `/api/tags` 200 | curl + cron |
| ChromaDB | heartbeat | curl + cron |
| Host | CPU/RAM/Disco/GPU | node_exporter / Glances |

## 2. Métricas clave (definidas en `MetricasIA`)
- Tiempo promedio de respuesta (generación IA).
- Tiempo de recuperación RAG.
- Tiempo de consulta SQL.
- Tiempo de ejecución de Tools / Workflows.
- Errores (AuditoriaActividad.Resultado = Error).

## 3. Alertas (umbrales sugeridos)
| Alerta | Condición | Severidad |
|--------|-----------|-----------|
| Aplicación caída | `/health` != 200 por 60s | Crítica |
| Ollama no disponible | `/api/tags` falla 2 veces | Crítica |
| SQL Server no disponible | CanConnectAsync falla | Crítica |
| Disco lleno | > 90% uso | Alta |
| Exceso de errores | > 50 errores/5 min | Alta |
| Workflow fallido | EventoProcesado.Estado = Error | Media |
| Evento fallido | EventoProcesado con excepción | Media |
| Tiempo de respuesta excesivo | p95 > 30 s | Media |

## 4. Canales de notificación
- Correo / Teams / Slack (webhook).
- Registro en `Logs/` (Serilog).

## 5. Dashboard sugerido
Grafana apuntando a Prometheus con los paneles: requests/min, latencia p95, errores, uso CPU/RAM, estado de Ollama/SQL/Chroma.
