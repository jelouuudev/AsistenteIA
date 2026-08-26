-- Insertar documento del Glosario de Términos CloudSync Pro

-- 1. Insertar en tabla Documento
INSERT INTO Documento (Codigo, Nombre, Descripcion, IdCategoria, VersionActual, Estado, PendienteProcesamiento, FechaRegistro, UsuarioRegistro)
VALUES ('GLOS-001', 'Glosario de Términos CloudSync Pro', 'Definiciones de los términos técnicos-utilizados en el manual de CloudSync Pro', 1, 1, 'Activo', 1, GETUTCDATE(), 1);

-- Obtener el IdDocumento insertado
DECLARE @IdDocumento INT;
SELECT @IdDocumento = SCOPE_IDENTITY();

-- 2. Insertar en tabla DocumentoVersion
INSERT INTO DocumentoVersion (IdDocumento, NumeroVersion, NombreArchivo, RutaArchivo, TamanoArchivo, HashArchivo, FechaCarga, UsuarioCarga, Activo)
VALUES (@IdDocumento, 1, 'Manual_Producto_CloudSync_Pro_Glosario.txt', 'C:\AsistenteIA_Documentos\Manual_Producto_CloudSync_Pro_Glosario.txt', 1250, NULL, GETUTCDATE(), 1, 1);

PRINT 'Documento del Glosario insertado correctamente. IdDocumento: ' + CAST(@IdDocumento AS VARCHAR);
