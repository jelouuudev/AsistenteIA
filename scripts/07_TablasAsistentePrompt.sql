-- ============================================
-- Script: 07_TablasAsistentePrompt.sql
-- Proyecto: Asistente Inteligente Empresarial
-- Descripción: Creación de tablas para el motor
--              de configuración del asistente
--              (Asistente, PromptSistema, HistorialPrompt)
-- ============================================

USE [AsistenteIA];
GO

-- ============================================
-- Tabla: Asistente
-- Descripción: Almacena la configuración de
--              cada asistente IA
-- ============================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Asistente]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[Asistente] (
        [IdAsistente]            INT              IDENTITY(1,1) NOT NULL,
        [Nombre]                 NVARCHAR(100)    NOT NULL,
        [Descripcion]            NVARCHAR(500)    NULL,
        [ModeloIA]               NVARCHAR(100)    NOT NULL DEFAULT N'deepseek-r1:7b',
        [Activo]                 BIT              NOT NULL DEFAULT 1,
        [FechaCreacion]          DATETIME2(7)     NOT NULL DEFAULT SYSUTCDATETIME(),
        [Idioma]                 NVARCHAR(10)     NULL DEFAULT N'es',
        [LongitudMaximaRespuesta] INT             NULL,
        [NivelFormalidad]        NVARCHAR(20)     NULL DEFAULT N'profesional',
        [FormatoRespuesta]       NVARCHAR(20)     NULL DEFAULT N'texto',
        [Restricciones]          NVARCHAR(MAX)    NULL,
        [MensajeBienvenida]      NVARCHAR(500)    NULL,
        [Temperatura]            FLOAT            NULL DEFAULT 0.7,
        [MaxTokens]              INT              NULL DEFAULT 4096,
        [TimeoutSegundos]        INT              NULL DEFAULT 300,
        CONSTRAINT [PK_Asistente] PRIMARY KEY CLUSTERED ([IdAsistente] ASC)
    );
    PRINT 'Tabla Asistente creada exitosamente.';
END
ELSE
    PRINT 'La tabla Asistente ya existe.';
GO

-- ============================================
-- Tabla: PromptSistema
-- Descripción: Almacena los prompts del sistema
--              asociados a cada asistente
-- ============================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[PromptSistema]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[PromptSistema] (
        [IdPrompt]       INT              IDENTITY(1,1) NOT NULL,
        [IdAsistente]    INT              NOT NULL,
        [Nombre]         NVARCHAR(200)    NOT NULL,
        [Contenido]      NVARCHAR(MAX)    NOT NULL,
        [Version]        INT              NOT NULL DEFAULT 1,
        [Activo]         BIT              NOT NULL DEFAULT 1,
        [FechaCreacion]  DATETIME2(7)     NOT NULL DEFAULT SYSUTCDATETIME(),
        [UsuarioCreacion] NVARCHAR(100)   NOT NULL DEFAULT N'Sistema',
        CONSTRAINT [PK_PromptSistema] PRIMARY KEY CLUSTERED ([IdPrompt] ASC),
        CONSTRAINT [FK_PromptSistema_Asistente] FOREIGN KEY ([IdAsistente])
            REFERENCES [dbo].[Asistente] ([IdAsistente])
            ON DELETE CASCADE
    );
    PRINT 'Tabla PromptSistema creada exitosamente.';
END
ELSE
    PRINT 'La tabla PromptSistema ya existe.';
GO

-- ============================================
-- Tabla: HistorialPrompt
-- Descripción: Historial de versiones de cada
--              prompt del sistema
-- ============================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[HistorialPrompt]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[HistorialPrompt] (
        [IdHistorial]        INT              IDENTITY(1,1) NOT NULL,
        [IdPrompt]           INT              NOT NULL,
        [Version]            INT              NOT NULL,
        [Contenido]          NVARCHAR(MAX)    NOT NULL,
        [FechaModificacion]  DATETIME2(7)     NOT NULL DEFAULT SYSUTCDATETIME(),
        [UsuarioModificacion] NVARCHAR(100)   NOT NULL DEFAULT N'Sistema',
        [MotivoCambio]       NVARCHAR(500)    NULL,
        CONSTRAINT [PK_HistorialPrompt] PRIMARY KEY CLUSTERED ([IdHistorial] ASC),
        CONSTRAINT [FK_HistorialPrompt_PromptSistema] FOREIGN KEY ([IdPrompt])
            REFERENCES [dbo].[PromptSistema] ([IdPrompt])
            ON DELETE CASCADE
    );
    PRINT 'Tabla HistorialPrompt creada exitosamente.';
END
ELSE
    PRINT 'La tabla HistorialPrompt ya existe.';
GO

-- ============================================
-- Índices
-- ============================================
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'IX_PromptSistema_IdAsistente')
    CREATE NONCLUSTERED INDEX [IX_PromptSistema_IdAsistente]
        ON [dbo].[PromptSistema] ([IdAsistente])
        INCLUDE ([Nombre], [Version], [Activo]);
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'IX_HistorialPrompt_IdPrompt')
    CREATE NONCLUSTERED INDEX [IX_HistorialPrompt_IdPrompt]
        ON [dbo].[HistorialPrompt] ([IdPrompt])
        INCLUDE ([Version], [FechaModificacion]);
GO

PRINT 'Script 07 completado exitosamente.';
GO
