SET QUOTED_IDENTIFIER ON;
GO

-- Verificar estado completo del documento DOC-TEST
SELECT 
    d.IdDocumento,
    d.Codigo,
    d.Nombre,
    d.PendienteProcesamiento,
    dv.IdVersion,
    dv.NombreArchivo,
    dv.RutaArchivo,
    dp.IdDocumentoProcesado,
    dp.Estado,
    dp.FechaInicio,
    dp.FechaFin,
    dp.TotalCaracteres,
    dp.TotalChunks,
    di.IdDocumentoIndexado,
    di.Estado as EstadoIndexacion,
    di.FechaIndexacion,
    di.TotalEmbeddings
FROM Documento d
LEFT JOIN DocumentoVersion dv ON d.IdDocumento = dv.IdDocumento AND dv.Activo = 1
LEFT JOIN DocumentoProcesado dp ON dv.IdVersion = dp.IdVersionDocumento
LEFT JOIN DocumentoIndexado di ON dp.IdDocumentoProcesado = di.IdDocumentoProcesado
WHERE d.Codigo = 'DOC-TEST';
