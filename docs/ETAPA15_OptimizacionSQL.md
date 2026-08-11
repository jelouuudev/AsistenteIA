# Optimización SQL Server — ETAPA 15 (Actividad 15)

## Revisiones aplicadas / recomendadas
- **Consultas parametrizadas:** todo acceso vía EF Core / `SqlParameter` (sin concatenación). Verificado en `SqlQueryTool`.
- **Validación de entradas:** el orquestador valida permiso y estructura antes de ejecutar.
- **Restricción de operaciones:** `SqlQueryTool` permite solo `SELECT`; bloquea DDL/DML.
- **Límite de resultados:** `MaximoRegistros=100` en `MotorConsultas`.
- **Consultas simultáneas:** `MaxConsultasSimultaneas=5`.

## Índices sugeridos (ejecutar en producción)
```sql
CREATE NONCLUSTERED INDEX IX_UsuarioAsistentes_IdUsuario ON UsuarioAsistentes(IdUsuario) INCLUDE(IdAsistente, Activo);
CREATE NONCLUSTERED INDEX IX_UsuarioFuentes_IdUsuario ON UsuarioFuentes(IdUsuario) INCLUDE(IdFuente, Activo);
CREATE NONCLUSTERED INDEX IX_AuditoriaActividad_Fecha ON AuditoriaActividad(Fecha);
CREATE NONCLUSTERED INDEX IX_ChunkDocumento_IdDocumento ON ChunkDocumento(IdDocumento);
CREATE NONCLUSTERED INDEX IX_EventoProcesado_Estado ON EventoProcesado(Estado);
```

## Planes de ejecución
- Usar `SET STATISTICS IO, TIME ON` para identificar scans en tablas grandes (AuditoriaActividad, ChunkDocumento).
- Considerar particionar `AuditoriaActividad` por fecha en entornos de alto volumen.

## Consultas lentas a vigilar
- Recuperación RAG (join ChunkDocumento ↔ Collection Chroma) — depende de ChromaDB.
- Reportes de auditoría con rangos amplios — acotar por fecha y usar índices sugeridos.
