-- ============================================
-- SCRIPT DE LIMPIEZA TOTAL - DOCUMENTOS TEST
-- ============================================
-- Este script elimina TODOS los documentos procesados de la base de datos
-- ADVERTENCIA: Esta acción es IRREVERSIBLE
-- ============================================

USE AsistenteDB;
GO

-- ============================================
-- PASO 1: Verificar cuántos documentos se eliminarán
-- ============================================
SELECT 'Documentos a eliminar:' AS Info, COUNT(*) AS Cantidad FROM Documentos;
SELECT 'Versiones a eliminar:' AS Info, COUNT(*) AS Cantidad FROM DocumentoVersiones;
SELECT 'Procesamientos a eliminar:' AS Info, COUNT(*) AS Cantidad FROM DocumentosProcesados;
SELECT 'Chunks a eliminar:' AS Info, COUNT(*) AS Cantidad FROM DocumentoChunks;
SELECT 'Indexaciones a eliminar:' AS Info, COUNT(*) AS Cantidad FROM DocumentosIndexados;
SELECT 'Relaciones documento-fuente a eliminar:' AS Info, COUNT(*) AS Cantidad FROM DocumentosFuentes;
SELECT 'Auditorías documentales a eliminar:' AS Info, COUNT(*) AS Cantidad FROM AuditoriasDocumental;
GO

-- ============================================
-- PASO 2: Eliminar en orden correcto (respetando FK)
-- ============================================

-- 2.1 Eliminar chunks (depende de DocumentosProcesados)
DELETE FROM DocumentoChunks;
PRINT 'Chunks eliminados';
GO

-- 2.2 Eliminar documentos indexados (depende de DocumentosProcesados)
DELETE FROM DocumentosIndexados;
PRINT 'Documentos indexados eliminados';
GO

-- 2.3 Eliminar relaciones documento-fuente
DELETE FROM DocumentosFuentes;
PRINT 'Relaciones documento-fuente eliminadas';
GO

-- 2.4 Eliminar procesamiento documental
DELETE FROM DocumentosProcesados;
PRINT 'Procesamientos eliminados';
GO

-- 2.5 Eliminar auditorías documentales
DELETE FROM AuditoriasDocumental;
PRINT 'Auditorías documentales eliminadas';
GO

-- 2.6 Eliminar versiones de documentos
DELETE FROM DocumentoVersiones;
PRINT 'Versiones eliminadas';
GO

-- 2.7 Eliminar documentos
DELETE FROM Documentos;
PRINT 'Documentos eliminados';
GO

-- ============================================
-- PASO 3: Verificar limpieza
-- ============================================
SELECT 'Documentos restantes:' AS Info, COUNT(*) AS Cantidad FROM Documentos;
SELECT 'Versiones restantes:' AS Info, COUNT(*) AS Cantidad FROM DocumentoVersiones;
SELECT 'Procesamientos restantes:' AS Info, COUNT(*) AS Cantidad FROM DocumentosProcesados;
SELECT 'Chunks restantes:' AS Info, COUNT(*) AS Cantidad FROM DocumentoChunks;
SELECT 'Indexaciones restantes:' AS Info, COUNT(*) AS Cantidad FROM DocumentosIndexados;
GO

PRINT '============================================';
PRINT 'LIMPIEZA COMPLETADA EXITOSAMENTE';
PRINT '============================================';
PRINT 'IMPORTANTE: Ahora debes limpiar el vectorstore';
PRINT 'Ejecuta: POST /api/indexacion/limpiar-vectorstore';
PRINT 'O reinicia la aplicación para limpiar el archivo vectors.json';
GO
