# Plan de Recuperación ante Fallos (DR) — ETAPA 15

## 1. Objetivo
Recuperar la operación de la plataforma AsistenteIA tras fallos de: SQL Server, documentos, ChromaDB, configuración o aplicación Web/API.

## 2. RTO / RPO objetivo
- **RTO** (Recovery Time Objective): ≤ 60 min para componentes críticos.
- **RPO** (Recovery Point Objective): ≤ 15 min (backup de log cada 15 min).

## 3. Componentes y procedimientos

### 3.1 SQL Server
- **Backup**: `scripts/backup_full.sql` (diario, compresión) + `backup_diff.sql` (cada hora) + log (cada 15 min).
- **Recuperación**: `scripts/restore_sql.sql` (completo → diferencial → log).
- **Verificación**: `SELECT COUNT(*) FROM Asistente;` y login admin tras restaurar.

### 3.2 Documentos (PDF/TXT)
- Carpeta `C:\AsistenteIA_Documentos` respaldada por `scripts/backup_documents_config.ps1`.
- Recuperación: restaurar la carpeta y reindexar (la API reindexa automáticamente al iniciar).

### 3.3 ChromaDB (RAG)
- Datos en volumen `chroma_data` (Docker) o `C:\ChromaData`.
- Recuperación: restaurar el volumen; si no existe, regenerar embeddings desde documentos (la API indexa al arrancar).

### 3.4 Configuración
- `appsettings*.json`, `docker-compose.yml`, `.env` (solo en servidor), `VERSION` y `scripts/` en respaldo de config.
- Recuperación: copiar archivos y reiniciar servicios.

### 3.5 Aplicación Web / API
- Binarios vía Docker images versionadas o `dotnet publish`.
- Rollback: ver `ETAPA15_PlanRollback.md`.

## 4. Prueba de recuperación (mínimo 1 ejecutada)
1. Restaurar BD en instancia de prueba con `restore_sql.sql`.
2. Levantar API/Web contra esa BD.
3. Login admin + 1 chat de humo.
4. Registrar éxito/falla en bitácora.

## 5. Responsables
- DBA / Administrador: respaldos SQL.
- DevOps: Docker, configuración, despliegue.
