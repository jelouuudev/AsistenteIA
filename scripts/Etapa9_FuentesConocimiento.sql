-- =============================================
-- ETAPA 9: Sistema de Multiples Fuentes de Conocimiento
-- Script de Migracion SQL Server
-- =============================================

BEGIN TRANSACTION;

-- Tabla: FuenteConocimiento
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'FuenteConocimiento')
BEGIN
    CREATE TABLE FuenteConocimiento (
        IdFuente INT IDENTITY(1,1) NOT NULL,
        Nombre NVARCHAR(200) NOT NULL,
        Codigo NVARCHAR(50) NOT NULL,
        Descripcion NVARCHAR(1000) NULL,
        Tipo NVARCHAR(50) NOT NULL DEFAULT 'Manual',
        Activo BIT NOT NULL DEFAULT 1,
        Prioridad INT NOT NULL DEFAULT 5,
        FechaCreacion DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        UsuarioCreacion INT NOT NULL,
        CONSTRAINT PK_FuenteConocimiento PRIMARY KEY (IdFuente),
        CONSTRAINT UQ_FuenteConocimiento_Codigo UNIQUE (Codigo)
    );
    PRINT 'Tabla FuenteConocimiento creada.';
END

-- Tabla: AsistenteFuente
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'AsistenteFuente')
BEGIN
    CREATE TABLE AsistenteFuente (
        IdAsistente INT NOT NULL,
        IdFuente INT NOT NULL,
        Activo BIT NOT NULL DEFAULT 1,
        Prioridad INT NOT NULL DEFAULT 5,
        CONSTRAINT PK_AsistenteFuente PRIMARY KEY (IdAsistente, IdFuente),
        CONSTRAINT FK_AsistenteFuente_Asistente FOREIGN KEY (IdAsistente) REFERENCES Asistente(IdAsistente) ON DELETE CASCADE,
        CONSTRAINT FK_AsistenteFuente_Fuente FOREIGN KEY (IdFuente) REFERENCES FuenteConocimiento(IdFuente) ON DELETE CASCADE
    );
    PRINT 'Tabla AsistenteFuente creada.';
END

-- Tabla: DocumentoFuente
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'DocumentoFuente')
BEGIN
    CREATE TABLE DocumentoFuente (
        IdDocumento INT NOT NULL,
        IdFuente INT NOT NULL,
        Activo BIT NOT NULL DEFAULT 1,
        CONSTRAINT PK_DocumentoFuente PRIMARY KEY (IdDocumento, IdFuente),
        CONSTRAINT FK_DocumentoFuente_Documento FOREIGN KEY (IdDocumento) REFERENCES Documento(IdDocumento) ON DELETE CASCADE,
        CONSTRAINT FK_DocumentoFuente_Fuente FOREIGN KEY (IdFuente) REFERENCES FuenteConocimiento(IdFuente) ON DELETE CASCADE
    );
    PRINT 'Tabla DocumentoFuente creada.';
END

COMMIT TRANSACTION;
PRINT 'Migracion Etapa 9 completada exitosamente.';
