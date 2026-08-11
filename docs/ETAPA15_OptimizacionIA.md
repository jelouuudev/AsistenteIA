# Optimización del Modelo IA — ETAPA 15 (Actividad 13)

## Parámetros evaluados
| Parámetro | Valor dev | Valor prod sugerido | Nota |
|-----------|-----------|---------------------|------|
| Modelo | qwen2.5:14b (dev) / deepseek-r1:7b | deepseek-r1:7b | 7B corre en CPU/GPU modesta |
| Temperatura | 0.3 | 0.3 | Respuestas estables y deterministas |
| MaxTokens | 8192 | 8192 | Contexto amplio |
| Context Window | 16000 (LongitudMaximaContexto) | 16000 | Según modelo |
| Timeout | 3600 s | 3600 s | Generación local puede ser lenta |

## Determinación de configuración óptima
- En CPU: deepseek-r1:7b con `num_ctx` reducido mejora latencia; temperaturas bajas reducen alucinaciones.
- En GPU (recomendado producción): mantener 16000 ctx y 8192 max tokens.
- Medir tiempo promedio de respuesta en el servidor de producción (ver `ETAPA15_reporte_rendimiento_concurrencia.md`).

## Recomendación
- Producción: `deepseek-r1:7b`, Temperatura 0.3, MaxTokens 8192, TopK RAG 5, PuntajeMinimo 0.2.
- Mantener `PoliticasIA` para forzar modelo permitido y máximos por petición.
