USE AsistenteIA;
GO

-- Tabla Rol
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Rol' AND xtype='U')
BEGIN
    CREATE TABLE Rol (
        IdRol INT IDENTITY(1,1) PRIMARY KEY,
        Nombre NVARCHAR(50) NOT NULL UNIQUE,
        Descripcion NVARCHAR(250) NULL,
        Activo BIT NOT NULL DEFAULT 1
    );
END
GO

-- Tabla Usuario
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Usuario' AND xtype='U')
BEGIN
    CREATE TABLE Usuario (
        IdUsuario INT IDENTITY(1,1) PRIMARY KEY,
        Usuario NVARCHAR(50) NOT NULL UNIQUE,
        Nombres NVARCHAR(100) NOT NULL,
        Apellidos NVARCHAR(100) NOT NULL,
        Correo NVARCHAR(100) NOT NULL,
        PasswordHash NVARCHAR(256) NOT NULL,
        Activo BIT NOT NULL DEFAULT 1,
        FechaCreacion DATETIME2 NOT NULL DEFAULT GETDATE(),
        FechaUltimoAcceso DATETIME2 NULL
    );
END
GO

-- Tabla UsuarioRol
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='UsuarioRol' AND xtype='U')
BEGIN
    CREATE TABLE UsuarioRol (
        IdUsuario INT NOT NULL,
        IdRol INT NOT NULL,
        CONSTRAINT PK_UsuarioRol PRIMARY KEY (IdUsuario, IdRol),
        CONSTRAINT FK_UsuarioRol_Usuario FOREIGN KEY (IdUsuario) REFERENCES Usuario(IdUsuario) ON DELETE CASCADE,
        CONSTRAINT FK_UsuarioRol_Rol FOREIGN KEY (IdRol) REFERENCES Rol(IdRol) ON DELETE CASCADE
    );
END
GO

-- Tabla AuditoriaSesion
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='AuditoriaSesion' AND xtype='U')
BEGIN
    CREATE TABLE AuditoriaSesion (
        IdSesion INT IDENTITY(1,1) PRIMARY KEY,
        IdUsuario INT NOT NULL,
        FechaInicio DATETIME2 NOT NULL DEFAULT GETDATE(),
        FechaFin DATETIME2 NULL,
        DireccionIP NVARCHAR(50) NULL,
        Navegador NVARCHAR(250) NULL,
        Estado NVARCHAR(20) NOT NULL,
        CONSTRAINT FK_AuditoriaSesion_Usuario FOREIGN KEY (IdUsuario) REFERENCES Usuario(IdUsuario) ON DELETE CASCADE
    );
END
GO

-- Tabla AuditoriaActividad
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='AuditoriaActividad' AND xtype='U')
BEGIN
    CREATE TABLE AuditoriaActividad (
        IdActividad INT IDENTITY(1,1) PRIMARY KEY,
        IdUsuario INT NOT NULL,
        FechaHora DATETIME2 NOT NULL DEFAULT GETDATE(),
        Modulo NVARCHAR(50) NOT NULL,
        Accion NVARCHAR(100) NOT NULL,
        Descripcion NVARCHAR(500) NOT NULL,
        DireccionIP NVARCHAR(50) NULL,
        CONSTRAINT FK_AuditoriaActividad_Usuario FOREIGN KEY (IdUsuario) REFERENCES Usuario(IdUsuario) ON DELETE CASCADE
    );
END
GO
