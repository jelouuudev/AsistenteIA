-- ============================================================
-- ETAPA 15 - Procedimiento de Recuperación SQL Server
-- Restaura el último backup completo + diferencial + logs.
-- Ajustar rutas y fechas según los archivos disponibles.
-- ============================================================
-- 1) Restaurar BACKUP COMPLETO (WITH NORECOVERY para encadenar)
RESTORE DATABASE [AsistenteIA]
    FROM DISK = N'C:\Backups\SQL\AsistenteIA_Full_YYYYMMDD_HHMMSS.bak'
    WITH NORECOVERY, REPLACE, STATS = 10;
PRINT 'Restaurado backup completo.';

-- 2) Restaurar BACKUP DIFERENCIAL (si existe, WITH NORECOVERY)
RESTORE DATABASE [AsistenteIA]
    FROM DISK = N'C:\Backups\SQL\AsistenteIA_Diff_YYYYMMDD_HHMMSS.bak'
    WITH NORECOVERY, STATS = 10;
PRINT 'Restaurado backup diferencial.';

-- 3) Restaurar BACKUP DE LOG (en orden, WITH RECOVERY en el último)
RESTORE LOG [AsistenteIA]
    FROM DISK = N'C:\Backups\SQL\AsistenteIA_Log_YYYYMMDD_HHMMSS.trn'
    WITH RECOVERY, STATS = 10;
PRINT 'Recuperación completa finalizada.';
