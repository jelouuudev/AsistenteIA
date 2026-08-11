-- ============================================================
-- ETAPA 15 - Script de Backup Diferencial SQL Server
-- Complementa backup_full.sql. Ejecutar periódicamente (ej. cada hora).
-- ============================================================
DECLARE @Fecha NVARCHAR(20) = REPLACE(REPLACE(REPLACE(CONVERT(NVARCHAR, GETDATE(), 120), '-', ''), ' ', '_'), ':', '');
DECLARE @Ruta NVARCHAR(256) = 'C:\Backups\SQL\';
DECLARE @Archivo NVARCHAR(256) = @Ruta + 'AsistenteIA_Diff_' + @Fecha + '.bak';

BACKUP DATABASE [AsistenteIA]
    TO DISK = @Archivo
    WITH DIFFERENTIAL, COMPRESSION, STATS = 10, INIT,
         NAME = 'AsistenteIA Differential Backup';
PRINT 'Backup diferencial: ' + @Archivo;
