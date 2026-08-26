SET QUOTED_IDENTIFIER ON;
GO

-- =============================================
-- Script: reprocesar_todos.sql
-- Descripción: Fuerza el reprocesamiento de TODOS los documentos.
--   1. Marca cada documento como PendienteProcesamiento = 1
--   2. Borra DocumentoIndexado (embeddings en ChromaDB / vector store)
--   3. Borra DocumentoChunk (chunks de texto)
--   4. Borra DocumentoProcesado (registros de procesamiento)
--
-- Uso: Ejecutar después de compilar la API con los fixes nuevos.
-- La API procesará automáticamente los documentos pendientes al iniciar.
-- =============================================

USE AsistenteIA;
GO

PRINT '==========================================';
PRINT ' REPROCESAMIENTO MASIVO DE DOCUMENTOS';
PRINT ' Fecha: ' + CONVERT(NVARCHAR(20), GETDATE(), 120);
PRINT '==========================================';
PRINT '';

-- =============================================
-- PASO 0: Ver estado actual
-- =============================================
PRINT '--- Estado ANTES del reprocesamiento ---';

DECLARE @totalDocs INT, @totalChunks INT, @totalIndexados INT, @totalProcesados INT;

SELECT @totalDocs = COUNT(*) FROM Documento;
SELECT @totalChunks = COUNT(*) FROM DocumentoChunk;
SELECT @totalIndexados = COUNT(*) FROM DocumentoIndexado;
SELECT @totalProcesados = COUNT(*) FROM DocumentoProcesado;

PRINT '  Documentos:          ' + CAST(@totalDocs AS NVARCHAR(10));
PRINT '  Chunks:              ' + CAST(@totalChunks AS NVARCHAR(10));
PRINT '  Indexaciones:        ' + CAST(@totalIndexados AS NVARCHAR(10));
PRINT '  Registros procesados: ' + CAST(@totalProcesados AS NVARCHAR(10));
PRINT '';

-- Listar documentos existentes
SELECT 
    d.IdDocumento AS [ID],
    d.Codigo AS [Codigo],
    d.Nombre AS [Nombre],
    d.PendienteProcesamiento AS [Pendiente],
    CASE WHEN dv.IdVersion IS NOT NULL 
         THEN SUBSTRING(dv.RutaArchivo, CHARINDEX('\', dv.RutaArchivo, LEN('C:\AsistenteIA_Documentos') + 2) + 1, 200)
         ELSE '(sin version)' END AS [Archivo]
FROM Documento d
LEFT JOIN DocumentoVersion dv ON d.IdDocumento = dv.IdDocumento AND dv.Activo = 1
ORDER BY d.IdDocumento;
PRINT '';

-- =============================================
-- PASO 1: Marcar todos como pendientes
-- =============================================
PRINT '--- PASO 1: Marcando documentos como pendientes ---';

UPDATE Documento
SET PendienteProcesamiento = 1;

PRINT '  OK: ' + CAST(@@ROWCOUNT AS NVARCHAR(10)) + ' documentos marcados.';
PRINT '';

-- =============================================
-- PASO 2: Borrar DocumentoIndexado (embeddings)
-- =============================================
PRINT '--- PASO 2: Borrando indexaciones (embeddings) ---';

DELETE di
FROM DocumentoIndexado di;

PRINT '  OK: ' + CAST(@@ROWCOUNT AS NVARCHAR(10)) + ' registros de indexación borrados.';
PRINT '';

-- =============================================
-- PASO 3: Borrar DocumentoChunk (texto dividido)
-- =============================================
PRINT '--- PASO 3: Borrando chunks ---';

DELETE FROM DocumentoChunk;

PRINT '  OK: ' + CAST(@@ROWCOUNT AS NVARCHAR(10)) + ' chunks borrados.';
PRINT '';

-- =============================================
-- PASO 4: Borrar DocumentoProcesado
-- =============================================
PRINT '--- PASO 4: Borrando registros de procesamiento ---';

DELETE FROM DocumentoProcesado;

PRINT '  OK: ' + CAST(@@ROWCOUNT AS NVARCHAR(10)) + ' registros de procesamiento borrados.';
PRINT '';

-- =============================================
-- PASO 4B: Recrear registros de procesamiento PENDIENTES
-- (El servicio de procesamiento automático solo actúa sobre
--  filas de DocumentoProcesado con Estado='Pendiente'.
--  Si se borran sin recrearlas, ningún documento se reprocesa.)
-- =============================================
PRINT '--- PASO 4B: Creando registros DocumentoProcesado (Pendiente) ---';

INSERT INTO DocumentoProcesado (IdVersionDocumento, FechaInicio, Estado, Observaciones)
SELECT dv.IdVersion, GETDATE(), 'Pendiente', 'Reprocesamiento masivo'
FROM Documento d
JOIN DocumentoVersion dv
  ON d.IdDocumento = dv.IdDocumento
 AND dv.Activo = 1
 AND dv.NumeroVersion = d.VersionActual
WHERE d.PendienteProcesamiento = 1
  AND NOT EXISTS (
      SELECT 1 FROM DocumentoProcesado dp
      WHERE dp.IdVersionDocumento = dv.IdVersion
  );

PRINT '  OK: ' + CAST(@@ROWCOUNT AS NVARCHAR(10)) + ' registros de procesamiento (Pendiente) creados.';
PRINT '';

-- =============================================
-- PASO 5: Verificar limpieza
-- =============================================
PRINT '--- Estado DESPUÉS del reprocesamiento ---';

DECLARE @chunksPost INT, @indexPost INT, @procPost INT, @pendientes INT;

SELECT @chunksPost = COUNT(*) FROM DocumentoChunk;
SELECT @indexPost = COUNT(*) FROM DocumentoIndexado;
SELECT @procPost = COUNT(*) FROM DocumentoProcesado;
SELECT @pendientes = COUNT(*) FROM Documento WHERE PendienteProcesamiento = 1;

PRINT '  Chunks:              ' + CAST(@chunksPost AS NVARCHAR(10));
PRINT '  Indexaciones:        ' + CAST(@indexPost AS NVARCHAR(10));
PRINT '  Registros procesados: ' + CAST(@procPost AS NVARCHAR(10)) + ' (deben ser Pendiente)';
PRINT '  Pendientes:          ' + CAST(@pendientes AS NVARCHAR(10)) + '/' + CAST(@totalDocs AS NVARCHAR(10));
PRINT '';

IF @chunksPost = 0 AND @indexPost = 0 AND @procPost > 0 AND @pendientes = @totalDocs
    PRINT '  OK REPROCESAMIENTO PROGRAMADO. La API reprocesara ' + CAST(@procPost AS NVARCHAR(10)) + ' documentos automaticamente.';
ELSE
    PRINT '  ADVERTENCIA: Revise los contadores (chunks=' + CAST(@chunksPost AS NVARCHAR(10)) + ', index=' + CAST(@indexPost AS NVARCHAR(10)) + ', proc=' + CAST(@procPost AS NVARCHAR(10)) + ').';
PRINT '';

-- =============================================
-- INSTRUCCIONES
-- =============================================
PRINT '==========================================';
PRINT ' SIGUIENTES PASOS:';
PRINT '==========================================';
PRINT '';
PRINT '  1. Detener la API (Ctrl+C en la consola)';
PRINT '  2. Compilar: dotnet build AsistenteIA.slnx';
PRINT '  3. Limpiar ChromaDB (PowerShell):';
PRINT '     Invoke-RestMethod -Method DELETE http://localhost:8000/api/v1/collections/asistente_documentos';
PRINT '  4. Iniciar la API: dotnet run --project Asistente.API';
PRINT '  5. La API procesará automáticamente los documentos pendientes';
PRINT '  6. Verificar en la interfaz Web -> Documentos -> Estado';
PRINT '';
PRINT '  NOTA: Si la collection de ChromaDB no existía, se creará automáticamente.';
PRINT '  El procesamiento puede tardar 1-5 min dependiendo del número de documentos.';
PRINT '';
PRINT '==========================================';
PRINT ' Fin: ' + CONVERT(NVARCHAR(20), GETDATE(), 120);
PRINT '==========================================';
GO
