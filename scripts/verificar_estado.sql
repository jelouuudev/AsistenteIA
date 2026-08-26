SET QUOTED_IDENTIFIER ON;
GO

-- Verificar estado del documento Guia Desarrollo Software
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
    dp.Observaciones
FROM Documento d
LEFT JOIN DocumentoVersion dv ON d.IdDocumento = dv.IdDocumento AND dv.Activo = 1
LEFT JOIN DocumentoProcesado dp ON dv.IdVersion = dp.IdVersionDocumento
WHERE d.IdDocumento = 12;
