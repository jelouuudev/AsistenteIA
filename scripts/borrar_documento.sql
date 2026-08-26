SET QUOTED_IDENTIFIER ON;
GO

-- Borrar documento DOC-CLOUD-001 (Manual CloudSync Pro)

-- 1. Borrar indexaciones
DELETE FROM DocumentoIndexado 
WHERE IdDocumentoProcesado IN (
    SELECT dp.IdDocumentoProcesado 
    FROM DocumentoProcesado dp
    INNER JOIN DocumentoVersion dv ON dp.IdVersionDocumento = dv.IdVersion
    WHERE dv.IdDocumento = 11
);

-- 2. Borrar chunks
DELETE FROM DocumentoChunk 
WHERE IdDocumentoProcesado IN (
    SELECT dp.IdDocumentoProcesado 
    FROM DocumentoProcesado dp
    INNER JOIN DocumentoVersion dv ON dp.IdVersionDocumento = dv.IdVersion
    WHERE dv.IdDocumento = 11
);

-- 3. Borrar procesamientos
DELETE FROM DocumentoProcesado 
WHERE IdVersionDocumento IN (
    SELECT IdVersion FROM DocumentoVersion WHERE IdDocumento = 11
);

-- 4. Borrar versiones
DELETE FROM DocumentoVersion WHERE IdDocumento = 11;

-- 5. Borrar documento
DELETE FROM Documento WHERE IdDocumento = 11;

PRINT 'Documento DOC-CLOUD-001 borrado correctamente.';
