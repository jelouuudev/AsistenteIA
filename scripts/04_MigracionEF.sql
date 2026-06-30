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

