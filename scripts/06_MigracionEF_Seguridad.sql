IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260627203330_InitialCreate'
)
BEGIN
    CREATE TABLE [Conversacion] (
        [IdConversacion] int NOT NULL IDENTITY,
        [FechaInicio] datetime2 NOT NULL,
        [FechaFin] datetime2 NULL,
        [Estado] nvarchar(20) NOT NULL,
        CONSTRAINT [PK_Conversacion] PRIMARY KEY ([IdConversacion])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260627203330_InitialCreate'
)
BEGIN
    CREATE TABLE [Mensaje] (
        [IdMensaje] int NOT NULL IDENTITY,
        [IdConversacion] int NOT NULL,
        [Rol] nvarchar(20) NOT NULL,
        [Contenido] nvarchar(max) NOT NULL,
        [FechaHora] datetime2 NOT NULL,
        [TiempoRespuestaMs] bigint NULL,
        CONSTRAINT [PK_Mensaje] PRIMARY KEY ([IdMensaje]),
        CONSTRAINT [FK_Mensaje_Conversacion_IdConversacion] FOREIGN KEY ([IdConversacion]) REFERENCES [Conversacion] ([IdConversacion]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260627203330_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Mensaje_IdConversacion] ON [Mensaje] ([IdConversacion]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260627203330_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260627203330_InitialCreate', N'8.0.0');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706190938_AddSecurityAndAuditing'
)
BEGIN
    CREATE TABLE [Rol] (
        [IdRol] int NOT NULL IDENTITY,
        [Nombre] nvarchar(50) NOT NULL,
        [Descripcion] nvarchar(250) NULL,
        [Activo] bit NOT NULL DEFAULT CAST(1 AS bit),
        CONSTRAINT [PK_Rol] PRIMARY KEY ([IdRol])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706190938_AddSecurityAndAuditing'
)
BEGIN
    CREATE TABLE [Usuario] (
        [IdUsuario] int NOT NULL IDENTITY,
        [Usuario] nvarchar(50) NOT NULL,
        [Nombres] nvarchar(100) NOT NULL,
        [Apellidos] nvarchar(100) NOT NULL,
        [Correo] nvarchar(100) NOT NULL,
        [PasswordHash] nvarchar(256) NOT NULL,
        [Activo] bit NOT NULL DEFAULT CAST(1 AS bit),
        [FechaCreacion] datetime2 NOT NULL,
        [FechaUltimoAcceso] datetime2 NULL,
        CONSTRAINT [PK_Usuario] PRIMARY KEY ([IdUsuario])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706190938_AddSecurityAndAuditing'
)
BEGIN
    CREATE TABLE [AuditoriaActividad] (
        [IdActividad] int NOT NULL IDENTITY,
        [IdUsuario] int NOT NULL,
        [FechaHora] datetime2 NOT NULL,
        [Modulo] nvarchar(50) NOT NULL,
        [Accion] nvarchar(100) NOT NULL,
        [Descripcion] nvarchar(500) NOT NULL,
        [DireccionIP] nvarchar(50) NULL,
        CONSTRAINT [PK_AuditoriaActividad] PRIMARY KEY ([IdActividad]),
        CONSTRAINT [FK_AuditoriaActividad_Usuario_IdUsuario] FOREIGN KEY ([IdUsuario]) REFERENCES [Usuario] ([IdUsuario]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706190938_AddSecurityAndAuditing'
)
BEGIN
    CREATE TABLE [AuditoriaSesion] (
        [IdSesion] int NOT NULL IDENTITY,
        [IdUsuario] int NOT NULL,
        [FechaInicio] datetime2 NOT NULL,
        [FechaFin] datetime2 NULL,
        [DireccionIP] nvarchar(50) NULL,
        [Navegador] nvarchar(250) NULL,
        [Estado] nvarchar(20) NOT NULL,
        CONSTRAINT [PK_AuditoriaSesion] PRIMARY KEY ([IdSesion]),
        CONSTRAINT [FK_AuditoriaSesion_Usuario_IdUsuario] FOREIGN KEY ([IdUsuario]) REFERENCES [Usuario] ([IdUsuario]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706190938_AddSecurityAndAuditing'
)
BEGIN
    CREATE TABLE [UsuarioRol] (
        [IdUsuario] int NOT NULL,
        [IdRol] int NOT NULL,
        CONSTRAINT [PK_UsuarioRol] PRIMARY KEY ([IdUsuario], [IdRol]),
        CONSTRAINT [FK_UsuarioRol_Rol_IdRol] FOREIGN KEY ([IdRol]) REFERENCES [Rol] ([IdRol]) ON DELETE CASCADE,
        CONSTRAINT [FK_UsuarioRol_Usuario_IdUsuario] FOREIGN KEY ([IdUsuario]) REFERENCES [Usuario] ([IdUsuario]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706190938_AddSecurityAndAuditing'
)
BEGIN
    CREATE INDEX [IX_AuditoriaActividad_IdUsuario] ON [AuditoriaActividad] ([IdUsuario]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706190938_AddSecurityAndAuditing'
)
BEGIN
    CREATE INDEX [IX_AuditoriaSesion_IdUsuario] ON [AuditoriaSesion] ([IdUsuario]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706190938_AddSecurityAndAuditing'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Rol_Nombre] ON [Rol] ([Nombre]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706190938_AddSecurityAndAuditing'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Usuario_Usuario] ON [Usuario] ([Usuario]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706190938_AddSecurityAndAuditing'
)
BEGIN
    CREATE INDEX [IX_UsuarioRol_IdRol] ON [UsuarioRol] ([IdRol]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260706190938_AddSecurityAndAuditing'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260706190938_AddSecurityAndAuditing', N'8.0.0');
END;
GO

COMMIT;
GO

