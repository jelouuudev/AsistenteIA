SET QUOTED_IDENTIFIER ON;
GO

-- Obtener ID de la versión activa del documento Guia Desarrollo Software
SELECT dv.IdVersion, dv.NombreArchivo, dv.RutaArchivo
FROM DocumentoVersion dv
INNER JOIN Documento d ON dv.IdDocumento = d.IdDocumento
WHERE d.IdDocumento = 12 AND dv.Activo = 1;
