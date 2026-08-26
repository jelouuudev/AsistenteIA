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

CREATE TABLE [Conversacion] (
    [IdConversacion] int NOT NULL IDENTITY,
    [FechaInicio] datetime2 NOT NULL,
    [FechaFin] datetime2 NULL,
    [Estado] nvarchar(20) NOT NULL,
    CONSTRAINT [PK_Conversacion] PRIMARY KEY ([IdConversacion])
);
GO

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
GO

CREATE INDEX [IX_Mensaje_IdConversacion] ON [Mensaje] ([IdConversacion]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260627203330_InitialCreate', N'8.0.0');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

CREATE TABLE [Rol] (
    [IdRol] int NOT NULL IDENTITY,
    [Nombre] nvarchar(50) NOT NULL,
    [Descripcion] nvarchar(250) NULL,
    [Activo] bit NOT NULL DEFAULT CAST(1 AS bit),
    CONSTRAINT [PK_Rol] PRIMARY KEY ([IdRol])
);
GO

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
GO

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
GO

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
GO

CREATE TABLE [UsuarioRol] (
    [IdUsuario] int NOT NULL,
    [IdRol] int NOT NULL,
    CONSTRAINT [PK_UsuarioRol] PRIMARY KEY ([IdUsuario], [IdRol]),
    CONSTRAINT [FK_UsuarioRol_Rol_IdRol] FOREIGN KEY ([IdRol]) REFERENCES [Rol] ([IdRol]) ON DELETE CASCADE,
    CONSTRAINT [FK_UsuarioRol_Usuario_IdUsuario] FOREIGN KEY ([IdUsuario]) REFERENCES [Usuario] ([IdUsuario]) ON DELETE CASCADE
);
GO

CREATE INDEX [IX_AuditoriaActividad_IdUsuario] ON [AuditoriaActividad] ([IdUsuario]);
GO

CREATE INDEX [IX_AuditoriaSesion_IdUsuario] ON [AuditoriaSesion] ([IdUsuario]);
GO

CREATE UNIQUE INDEX [IX_Rol_Nombre] ON [Rol] ([Nombre]);
GO

CREATE UNIQUE INDEX [IX_Usuario_Usuario] ON [Usuario] ([Usuario]);
GO

CREATE INDEX [IX_UsuarioRol_IdRol] ON [UsuarioRol] ([IdRol]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260706190938_AddSecurityAndAuditing', N'8.0.0');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

CREATE TABLE [Asistente] (
    [IdAsistente] int NOT NULL IDENTITY,
    [Nombre] nvarchar(100) NOT NULL,
    [Descripcion] nvarchar(500) NULL,
    [ModeloIA] nvarchar(50) NOT NULL DEFAULT N'deepseek-r1:7b',
    [Activo] bit NOT NULL DEFAULT CAST(1 AS bit),
    [FechaCreacion] datetime2 NOT NULL,
    [Idioma] nvarchar(20) NULL,
    [LongitudMaximaRespuesta] int NULL,
    [NivelFormalidad] nvarchar(50) NULL,
    [FormatoRespuesta] nvarchar(50) NULL,
    [Restricciones] nvarchar(1000) NULL,
    [MensajeBienvenida] nvarchar(500) NULL,
    [Temperatura] float NULL,
    [MaxTokens] int NULL,
    [TimeoutSegundos] int NULL,
    CONSTRAINT [PK_Asistente] PRIMARY KEY ([IdAsistente])
);
GO

CREATE TABLE [PromptSistema] (
    [IdPrompt] int NOT NULL IDENTITY,
    [IdAsistente] int NOT NULL,
    [Nombre] nvarchar(100) NOT NULL,
    [Contenido] nvarchar(max) NOT NULL,
    [Version] int NOT NULL,
    [Activo] bit NOT NULL DEFAULT CAST(1 AS bit),
    [FechaCreacion] datetime2 NOT NULL,
    [UsuarioCreacion] nvarchar(100) NOT NULL,
    CONSTRAINT [PK_PromptSistema] PRIMARY KEY ([IdPrompt]),
    CONSTRAINT [FK_PromptSistema_Asistente_IdAsistente] FOREIGN KEY ([IdAsistente]) REFERENCES [Asistente] ([IdAsistente]) ON DELETE CASCADE
);
GO

CREATE TABLE [HistorialPrompt] (
    [IdHistorial] int NOT NULL IDENTITY,
    [IdPrompt] int NOT NULL,
    [Version] int NOT NULL,
    [Contenido] nvarchar(max) NOT NULL,
    [FechaModificacion] datetime2 NOT NULL,
    [UsuarioModificacion] nvarchar(100) NOT NULL,
    [MotivoCambio] nvarchar(500) NULL,
    CONSTRAINT [PK_HistorialPrompt] PRIMARY KEY ([IdHistorial]),
    CONSTRAINT [FK_HistorialPrompt_PromptSistema_IdPrompt] FOREIGN KEY ([IdPrompt]) REFERENCES [PromptSistema] ([IdPrompt]) ON DELETE CASCADE
);
GO

CREATE INDEX [IX_HistorialPrompt_IdPrompt] ON [HistorialPrompt] ([IdPrompt]);
GO

CREATE INDEX [IX_PromptSistema_IdAsistente] ON [PromptSistema] ([IdAsistente]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260708024938_AddAsistenteEngine', N'8.0.0');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

ALTER TABLE [Conversacion] ADD [FechaUltimaActividad] datetime2 NULL;
GO

ALTER TABLE [Conversacion] ADD [ResumenContexto] nvarchar(max) NULL;
GO

ALTER TABLE [Conversacion] ADD [Titulo] nvarchar(200) NULL;
GO

ALTER TABLE [Conversacion] ADD [TotalMensajes] int NOT NULL DEFAULT 0;
GO

ALTER TABLE [Conversacion] ADD [UsuarioPropietario] int NOT NULL DEFAULT 0;
GO

DECLARE @var0 sysname;
SELECT @var0 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Asistente]') AND [c].[name] = N'ModeloIA');
IF @var0 IS NOT NULL EXEC(N'ALTER TABLE [Asistente] DROP CONSTRAINT [' + @var0 + '];');
ALTER TABLE [Asistente] ADD DEFAULT N'qwen2.5:7b' FOR [ModeloIA];
GO

CREATE TABLE [ConfiguracionMemoria] (
    [IdConfiguracion] int NOT NULL IDENTITY,
    [MaximoMensajesContexto] int NOT NULL DEFAULT 20,
    [MaximoTokensContexto] int NOT NULL DEFAULT 4096,
    [LongitudResumen] int NOT NULL DEFAULT 500,
    [CantidadConversacionesVisibles] int NOT NULL DEFAULT 50,
    [Activo] bit NOT NULL DEFAULT CAST(1 AS bit),
    CONSTRAINT [PK_ConfiguracionMemoria] PRIMARY KEY ([IdConfiguracion])
);
GO

CREATE INDEX [IX_Conversacion_FechaUltimaActividad] ON [Conversacion] ([FechaUltimaActividad]);
GO

CREATE INDEX [IX_Conversacion_UsuarioPropietario] ON [Conversacion] ([UsuarioPropietario]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260709235134_AddMemoriaYContexto', N'8.0.0');
GO

COMMIT;
GO

