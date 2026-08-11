-- ============================================================
-- ETAPA 15 - Script de Backup SQL Server (AsistenteIA)
-- Ejecutar con: sqlcmd -S <servidor> -E -C -i backup_full.sql
-- O desde SQL Server Management Studio.
-- ============================================================
DECLARE @Fecha NVARCHAR(20) = REPLACE(REPLACE(REPLACE(CONVERT(NVARCHAR, GETDATE(), 120), '-', ''), ' ', '_'), ':', '');
DECLARE @Ruta NVARCHAR(256) = 'C:\Backups\SQL\';
DECLARE @Archivo NVARCHAR(256);

-- 1) BACKUP COMPLETO
SET @Archivo = @Ruta + 'AsistenteIA_Full_' + @Fecha + '.bak';
BACKUP DATABASE [AsistenteIA]
    TO DISK = @Archivo
    WITH COMPRESSION, STATS = 10, INIT,
         NAME = 'AsistenteIA Full Backup';
PRINT 'Backup completo: ' + @Archivo;

-- 2) BACKUP DE LOG (requiere recovery model FULL)
SET @Archivo = @Ruta + 'AsistenteIA_Log_' + @Fecha + '.trn';
BACKUP LOG [AsistenteIA]
    TO DISK = @Archivo
    WITH STATS = 10, INIT,
         NAME = 'AsistenteIA Log Backup';
PRINT 'Backup de log: ' + @Archivo;
