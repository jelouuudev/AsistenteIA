-- Script para verificar y registrar documento_prueba2.pdf en la base de datos
-- Ejecutar en SQL Server Management Studio o Azure Data Studio

USE AsistenteDB;
GO

-- 1. Verificar si el documento ya existe
SELECT 
    d.IdDocumento,
    d.Codigo,
    d.Nombre,
    d.Estado,
    d.PendienteProcesamiento,
    v.IdVersion,
    v.NumeroVersion,
    v.NombreArchivo,
    v.RutaArchivo,
    dp.IdDocumentoProcesado,
    dp.Estado AS EstadoProcesamiento,
    dp.TotalChunks,
    di.IdDocumentoIndexado,
    di.Estado AS EstadoIndexacion,
    di.TotalEmbeddings
FROM Documentos d
LEFT JOIN DocumentoVersiones v ON d.IdDocumento = v.IdDocumento
LEFT JOIN DocumentosProcesados dp ON v.IdVersion = dp.IdVersionDocumento
LEFT JOIN DocumentosIndexados di ON dp.IdDocumentoProcesado = di.IdDocumentoProcesado
WHERE d.Nombre LIKE '%prueba2%' OR d.Codigo LIKE '%PRB002%' OR v.NombreArchivo LIKE '%documento_prueba2%';
GO

-- 2. Si no existe, registrar el documento
-- Primero, obtener o crear la categoría
DECLARE @IdCategoria INT;
SELECT @IdCategoria = IdCategoria FROM CategoriasDocumento WHERE Nombre = 'Políticas';

IF @IdCategoria IS NULL
BEGIN
    INSERT INTO CategoriasDocumento (Nombre, Descripcion, Activo, FechaCreacion)
    VALUES ('Políticas', 'Documentos de políticas corporativas', 1, GETUTCDATE());
    SET @IdCategoria = SCOPE_IDENTITY();
END

-- 3. Registrar el documento
DECLARE @IdDocumento INT;
INSERT INTO Documentos (Codigo, Nombre, IdCategoria, Descripcion, Estado, VersionActual, PendienteProcesamiento, FechaCreacion, FechaModificacion)
VALUES ('PRB002', 'Documento Prueba v2 - Política de Seguridad Informática', @IdCategoria, 
        'Política de Seguridad Informática v2.0 de TechCorp Solutions', 
        'Activo', 1, 1, GETUTCDATE(), GETUTCDATE());
SET @IdDocumento = SCOPE_IDENTITY();

-- 4. Registrar la versión del documento
DECLARE @IdVersion INT;
DECLARE @RutaArchivo NVARCHAR(500) = 'C:\Users\Raul\Desktop\AsistenteIA\documento_prueba2.pdf';
INSERT INTO DocumentoVersiones (IdDocumento, NumeroVersion, NombreArchivo, RutaArchivo, TamanoBytes, HashSHA256, FechaCreacion)
VALUES (@IdDocumento, 1, 'documento_prueba2.pdf', @RutaArchivo, 
        (SELECT size FROM sys.master_files WHERE physical_name = @RutaArchivo),
        'PENDIENTE_CALCULO', GETUTCDATE());
SET @IdVersion = SCOPE_IDENTITY();

-- 5. Registrar el procesamiento (estado Pendiente)
INSERT INTO DocumentosProcesados (IdVersionDocumento, Estado, FechaInicio, Observaciones, TotalPaginas, TotalCaracteres, TotalChunks)
VALUES (@IdVersion, 'Pendiente', GETUTCDATE(), 'Documento registrado manualmente. Pendiente de procesamiento.', 0, 0, 0);

PRINT 'Documento registrado exitosamente. El sistema lo procesará automáticamente en el próximo ciclo del servicio en segundo plano.';
GO

-- 6. Verificar el registro
SELECT 
    d.IdDocumento,
    d.Codigo,
    d.Nombre,
    d.Estado,
    d.PendienteProcesamiento,
    v.IdVersion,
    v.NumeroVersion,
    v.NombreArchivo,
    dp.IdDocumentoProcesado,
    dp.Estado AS EstadoProcesamiento
FROM Documentos d
INNER JOIN DocumentoVersiones v ON d.IdDocumento = v.IdDocumento
LEFT JOIN DocumentosProcesados dp ON v.IdVersion = dp.IdVersionDocumento
WHERE d.IdDocumento = SCOPE_IDENTITY();
GO
