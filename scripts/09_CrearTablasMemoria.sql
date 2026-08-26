-- =============================================
-- Script: 09_CrearTablasMemoria.sql
-- Proyecto: Asistente Inteligente Empresarial
-- Descripción: Crea las tablas necesarias para el
--              Motor de Memoria Conversacional
-- =============================================

-- Agregar columnas a la tabla Conversacion existente
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Conversacion') AND name = 'Titulo')
BEGIN
    ALTER TABLE Conversacion ADD Titulo NVARCHAR(200) NULL;
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Conversacion') AND name = 'UsuarioPropietario')
BEGIN
    ALTER TABLE Conversacion ADD UsuarioPropietario INT NOT NULL DEFAULT 0;
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Conversacion') AND name = 'FechaUltimaActividad')
BEGIN
    ALTER TABLE Conversacion ADD FechaUltimaActividad DATETIME2 NULL;
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Conversacion') AND name = 'ResumenContexto')
BEGIN
    ALTER TABLE Conversacion ADD ResumenContexto NVARCHAR(MAX) NULL;
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Conversacion') AND name = 'TotalMensajes')
BEGIN
    ALTER TABLE Conversacion ADD TotalMensajes INT NOT NULL DEFAULT 0;
END
GO

-- Actualizar el enum EstadoConversacion (se maneja como string, solo se agrega a la validación)
-- Los valores existentes 'Activa' y 'Finalizada' se mantienen
-- Se agregaron: 'Archivada', 'Eliminada'

-- Crear índices para mejorar rendimiento
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Conversacion_UsuarioPropietario')
BEGIN
    CREATE INDEX IX_Conversacion_UsuarioPropietario ON Conversacion(UsuarioPropietario);
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Conversacion_FechaUltimaActividad')
BEGIN
    CREATE INDEX IX_Conversacion_FechaUltimaActividad ON Conversacion(FechaUltimaActividad DESC);
END
GO

-- Crear tabla ConfiguracionMemoria
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID('ConfiguracionMemoria') AND type = 'U')
BEGIN
    CREATE TABLE ConfiguracionMemoria (
        IdConfiguracion INT IDENTITY(1,1) NOT NULL,
        MaximoMensajesContexto INT NOT NULL DEFAULT 20,
        MaximoTokensContexto INT NOT NULL DEFAULT 4096,
        LongitudResumen INT NOT NULL DEFAULT 500,
        CantidadConversacionesVisibles INT NOT NULL DEFAULT 50,
        Activo BIT NOT NULL DEFAULT 1,
        CONSTRAINT PK_ConfiguracionMemoria PRIMARY KEY (IdConfiguracion)
    );
END
GO

-- Insertar configuración por defecto si no existe
IF NOT EXISTS (SELECT 1 FROM ConfiguracionMemoria)
BEGIN
    INSERT INTO ConfiguracionMemoria (MaximoMensajesContexto, MaximoTokensContexto, LongitudResumen, CantidadConversacionesVisibles, Activo)
    VALUES (20, 4096, 500, 50, 1);
END
GO

PRINT 'Migración de Motor de Memoria Conversacional completada exitosamente.';
GO
