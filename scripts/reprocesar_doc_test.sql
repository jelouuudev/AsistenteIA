SET QUOTED_IDENTIFIER ON;
GO

-- Forzar reprocesamiento del documento Guia Desarrollo Software (DOC-TEST) sin borrarlo

-- 1. Marcar documento como pendiente de procesamiento
UPDATE Documento 
SET PendienteProcesamiento = 1 
WHERE Codigo = 'DOC-TEST';

-- 2. Borrar indexaciones anteriores
DELETE FROM DocumentoIndexado 
WHERE IdDocumentoProcesado IN (
    SELECT dp.IdDocumentoProcesado 
    FROM DocumentoProcesado dp
    INNER JOIN DocumentoVersion dv ON dp.IdVersionDocumento = dv.IdVersion
    INNER JOIN Documento d ON dv.IdDocumento = d.IdDocumento
    WHERE d.Codigo = 'DOC-TEST'
);

-- 3. Borrar chunks anteriores
DELETE FROM DocumentoChunk 
WHERE IdDocumentoProcesado IN (
    SELECT dp.IdDocumentoProcesado 
    FROM DocumentoProcesado dp
    INNER JOIN DocumentoVersion dv ON dp.IdVersionDocumento = dv.IdVersion
    INNER JOIN Documento d ON dv.IdDocumento = d.IdDocumento
    WHERE d.Codigo = 'DOC-TEST'
);

-- 4. Borrar registros de procesamiento anteriores
DELETE FROM DocumentoProcesado 
WHERE IdVersionDocumento IN (
    SELECT dv.IdVersion 
    FROM DocumentoVersion dv
    INNER JOIN Documento d ON dv.IdDocumento = d.IdDocumento
    WHERE d.Codigo = 'DOC-TEST'
);

PRINT 'Documento DOC-TEST marcado para reprocesamiento automatico.';
