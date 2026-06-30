USE AsistenteIA;
GO

-- Tabla Conversacion
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Conversacion' AND xtype='U')
BEGIN
    CREATE TABLE Conversacion (
        IdConversacion INT IDENTITY(1,1) PRIMARY KEY,
        FechaInicio DATETIME2 NOT NULL,
        FechaFin DATETIME2 NULL,
        Estado NVARCHAR(20) NOT NULL DEFAULT 'Activa'
    );
END
GO

-- Tabla Mensaje
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Mensaje' AND xtype='U')
BEGIN
    CREATE TABLE Mensaje (
        IdMensaje INT IDENTITY(1,1) PRIMARY KEY,
        IdConversacion INT NOT NULL,
        Rol NVARCHAR(20) NOT NULL,
        Contenido NVARCHAR(MAX) NOT NULL,
        FechaHora DATETIME2 NOT NULL,
        TiempoRespuestaMs BIGINT NULL,
        CONSTRAINT FK_Mensaje_Conversacion FOREIGN KEY (IdConversacion)
            REFERENCES Conversacion(IdConversacion)
            ON DELETE CASCADE
    );
END
GO
