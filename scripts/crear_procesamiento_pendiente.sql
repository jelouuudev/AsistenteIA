SET QUOTED_IDENTIFIER ON;
GO

-- Crear registro de procesamiento pendiente para DOC-TEST

-- Obtener ID de la versión activa del documento DOC-TEST
DECLARE @IdVersion INT;
SELECT @IdVersion = dv.IdVersion
FROM DocumentoVersion dv
INNER JOIN Documento d ON dv.IdDocumento = d.IdDocumento
WHERE d.Codigo = 'DOC-TEST' AND dv.Activo = 1;

-- Crear registro de procesamiento pendiente
INSERT INTO DocumentoProcesado (IdVersionDocumento, Estado, FechaInicio, Observaciones)
VALUES (@IdVersion, 'Pendiente', GETUTCDATE(), 'Pendiente de reprocesamiento tras limpieza de null bytes');

PRINT 'Registro de procesamiento pendiente creado para DOC-TEST.';
