SET QUOTED_IDENTIFIER ON;
GO

-- Forzar reprocesamiento del documento Guia Desarrollo Software (DOC-TEST, IdDocumento = 12)

-- 1. Marcar documento como pendiente de procesamiento
UPDATE Documento 
SET PendienteProcesamiento = 1 
WHERE IdDocumento = 12;

-- 2. Borrar procesamiento anterior
DELETE FROM DocumentoIndexado 
WHERE IdDocumentoProcesado IN (
    SELECT dp.IdDocumentoProcesado 
    FROM DocumentoProcesado dp
    INNER JOIN DocumentoVersion dv ON dp.IdVersionDocumento = dv.IdVersion
    WHERE dv.IdDocumento = 12
);

-- 3. Borrar chunks anteriores
DELETE FROM DocumentoChunk 
WHERE IdDocumentoProcesado IN (
    SELECT dp.IdDocumentoProcesado 
    FROM DocumentoProcesado dp
    INNER JOIN DocumentoVersion dv ON dp.IdVersionDocumento = dv.IdVersion
    WHERE dv.IdDocumento = 12
);

-- 4. Borrar registros de procesamiento
DELETE FROM DocumentoProcesado 
WHERE IdVersionDocumento IN (
    SELECT IdVersion FROM DocumentoVersion WHERE IdDocumento = 12
);

PRINT 'Documento Guia Desarrollo Software marcado para reprocesamiento.';
