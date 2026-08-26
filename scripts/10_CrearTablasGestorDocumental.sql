-- =============================================
-- Script: 10_CrearTablasGestorDocumental.sql
-- Proyecto: Asistente Inteligente Empresarial
-- Descripción: Crea las tablas para el Gestor Documental
--              y el control de versiones y auditoría.
-- =============================================

USE AsistenteIA;
GO

-- 1. Tabla CategoriaDocumento
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='CategoriaDocumento' AND xtype='U')
BEGIN
    CREATE TABLE CategoriaDocumento (
        IdCategoria INT IDENTITY(1,1) NOT NULL,
        Nombre NVARCHAR(100) NOT NULL,
        Descripcion NVARCHAR(500) NULL,
        Activo BIT NOT NULL CONSTRAINT DF_CategoriaDocumento_Activo DEFAULT 1,
        CONSTRAINT PK_CategoriaDocumento PRIMARY KEY (IdCategoria),
        CONSTRAINT UQ_CategoriaDocumento_Nombre UNIQUE (Nombre)
    );
END
GO

-- 2. Tabla Documento
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Documento' AND xtype='U')
BEGIN
    CREATE TABLE Documento (
        IdDocumento INT IDENTITY(1,1) NOT NULL,
        Codigo NVARCHAR(50) NOT NULL,
        Nombre NVARCHAR(200) NOT NULL,
        Descripcion NVARCHAR(1000) NULL,
        IdCategoria INT NOT NULL,
        VersionActual INT NOT NULL CONSTRAINT DF_Documento_VersionActual DEFAULT 0,
        Estado NVARCHAR(50) NOT NULL CONSTRAINT DF_Documento_Estado DEFAULT 'Borrador',
        PendienteProcesamiento BIT NOT NULL CONSTRAINT DF_Documento_Pendiente DEFAULT 1,
        FechaRegistro DATETIME2 NOT NULL,
        UsuarioRegistro INT NOT NULL,
        CONSTRAINT PK_Documento PRIMARY KEY (IdDocumento),
        CONSTRAINT UQ_Documento_Codigo UNIQUE (Codigo),
        CONSTRAINT FK_Documento_CategoriaDocumento FOREIGN KEY (IdCategoria)
            REFERENCES CategoriaDocumento(IdCategoria)
            ON DELETE NO ACTION
    );

    CREATE INDEX IX_Documento_IdCategoria ON Documento(IdCategoria);
    CREATE INDEX IX_Documento_Estado ON Documento(Estado);
END
GO

-- 3. Tabla DocumentoVersion
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='DocumentoVersion' AND xtype='U')
BEGIN
    CREATE TABLE DocumentoVersion (
        IdVersion INT IDENTITY(1,1) NOT NULL,
        IdDocumento INT NOT NULL,
        NumeroVersion INT NOT NULL,
        NombreArchivo NVARCHAR(255) NOT NULL,
        RutaArchivo NVARCHAR(1000) NOT NULL,
        TamanoArchivo BIGINT NOT NULL,
        HashArchivo NVARCHAR(64) NOT NULL,
        FechaCarga DATETIME2 NOT NULL,
        UsuarioCarga INT NOT NULL,
        Activo BIT NOT NULL CONSTRAINT DF_DocumentoVersion_Activo DEFAULT 1,
        CONSTRAINT PK_DocumentoVersion PRIMARY KEY (IdVersion),
        CONSTRAINT FK_DocumentoVersion_Documento FOREIGN KEY (IdDocumento)
            REFERENCES Documento(IdDocumento)
            ON DELETE CASCADE
    );

    CREATE INDEX IX_DocumentoVersion_IdDocumento ON DocumentoVersion(IdDocumento);
END
GO

-- 4. Tabla AuditoriaDocumental
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='AuditoriaDocumental' AND xtype='U')
BEGIN
    CREATE TABLE AuditoriaDocumental (
        IdAuditoria INT IDENTITY(1,1) NOT NULL,
        IdDocumento INT NOT NULL,
        IdVersion INT NULL,
        Accion NVARCHAR(50) NOT NULL,
        Descripcion NVARCHAR(1000) NOT NULL,
        UsuarioId INT NOT NULL,
        FechaAccion DATETIME2 NOT NULL,
        DireccionIP NVARCHAR(50) NULL,
        CONSTRAINT PK_AuditoriaDocumental PRIMARY KEY (IdAuditoria),
        CONSTRAINT FK_AuditoriaDocumental_Documento FOREIGN KEY (IdDocumento)
            REFERENCES Documento(IdDocumento)
            ON DELETE CASCADE
    );

    CREATE INDEX IX_AuditoriaDocumental_IdDocumento ON AuditoriaDocumental(IdDocumento);
END
GO

-- 5. Semilla de Categorías por defecto si no existen
IF NOT EXISTS (SELECT 1 FROM CategoriaDocumento)
BEGIN
    INSERT INTO CategoriaDocumento (Nombre, Descripcion, Activo)
    VALUES 
    (N'Manual Usuario', N'Manuales dirigidos a usuarios finales.', 1),
    (N'Manual Técnico', N'Documentación técnica de sistemas.', 1),
    (N'Procedimientos', N'Guías de procedimientos operativos y organizacionales.', 1),
    (N'Políticas', N'Líneas de conducta y políticas empresariales.', 1),
    (N'Normativas', N'Reglas, estándares y normativas aplicables.', 1),
    (N'FAQ', N'Preguntas frecuentes y respuestas rápidas.', 1),
    (N'Capacitaciones', N'Material y guías de capacitación del personal.', 1);
END
GO

PRINT 'Tablas del Gestor Documental creadas e inicializadas exitosamente.';
GO
