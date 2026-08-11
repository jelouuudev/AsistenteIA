# Optimización del RAG — ETAPA 15 (Actividad 14)

## Parámetros evaluados (appsettings `ProcesamientoDocumental` + `Embedding`)
| Parámetro | Valor dev | Valor prod sugerido | Efecto |
|-----------|-----------|---------------------|--------|
| Tamaño de chunk | 1000 | 1000 | Equilibrio contexto/precisión |
| Overlap | 200 | 200 | Evita cortar conceptos |
| Modelo embeddings | nomic-embed-text | nomic-embed-text | Ligero y multilingüe |
| TopK (docs recuperados) | 20 | 5 | Menos ruido en prod |
| Similarity threshold | 0.15 | 0.20 | Filtra recuperación irrelevante |
| Longitud máx contexto | 16000 | 16000 | Límite del modelo |

## Pruebas comparativas (recomendadas en producción)
1. Indexar 20 PDFs, consultar 10 preguntas representativas.
2. Medir tiempo de búsqueda (objetivo < 2 s) y relevancia (juicio humano).
3. Ajustar TopK/threshold hasta precisión aceptable sin ruido.

## Configuración seleccionada (producción)
- Chunk 1000, overlap 200, embeddings nomic-embed-text, TopK 5, threshold 0.20, ChromaDB persistente.

## Nota
- RAG requiere ChromaDB activo. En este host de desarrollo ChromaDB no está corriendo, por lo que las pruebas E2E de RAG quedan pendientes de ejecutar en el entorno con ChromaDB (ver `ETAPA15_reporte_funcional.md`).
