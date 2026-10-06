-- =====================================================================
-- ETAPA 20: API Gateway Empresarial y Framework de Conectores Externos
-- Tablas: Connectors, ConnectorConfiguraciones, ConnectorCredenciales,
--         ConnectorPoliticas, ConnectorEjecuciones
-- Equivalente a la migración EF 20261006184330_Etapa20_GatewayConectores.
-- Idempotente: solo crea lo que no existe.
-- =====================================================================

IF OBJECT_ID(N'[dbo].[Connectors]', N'U') IS NULL
BEGIN
CREATE TABLE [Connectors] (
    [IdConnector] int NOT NULL IDENTITY,
    [Codigo] nvarchar(100) NOT NULL,
    [Nombre] nvarchar(150) NOT NULL,
    [Tipo] nvarchar(30) NOT NULL DEFAULT N'REST',
    [Descripcion] nvarchar(500) NULL,
    [Activo] bit NOT NULL,
    [RequierePermiso] bit NOT NULL,
    [FechaRegistro] datetime2 NOT NULL,
    CONSTRAINT [PK_Connectors] PRIMARY KEY ([IdConnector])
);
CREATE UNIQUE INDEX [IX_Connectors_Codigo] ON [Connectors] ([Codigo]);
END
GO

IF OBJECT_ID(N'[dbo].[ConnectorConfiguraciones]', N'U') IS NULL
BEGIN
CREATE TABLE [ConnectorConfiguraciones] (
    [IdConfiguracion] int NOT NULL IDENTITY,
    [IdConnector] int NOT NULL,
    [Clave] nvarchar(100) NOT NULL,
    [Valor] nvarchar(1000) NOT NULL,
    [FechaRegistro] datetime2 NOT NULL,
    CONSTRAINT [PK_ConnectorConfiguraciones] PRIMARY KEY ([IdConfiguracion]),
    CONSTRAINT [FK_ConnectorConfiguraciones_Connectors_IdConnector] FOREIGN KEY ([IdConnector]) REFERENCES [Connectors] ([IdConnector]) ON DELETE CASCADE
);
CREATE UNIQUE INDEX [IX_ConnectorConfiguraciones_IdConnector_Clave] ON [ConnectorConfiguraciones] ([IdConnector], [Clave]);
END
GO

IF OBJECT_ID(N'[dbo].[ConnectorCredenciales]', N'U') IS NULL
BEGIN
CREATE TABLE [ConnectorCredenciales] (
    [IdCredencial] int NOT NULL IDENTITY,
    [IdConnector] int NOT NULL,
    [Tipo] nvarchar(20) NOT NULL DEFAULT N'None',
    [NombreUsuario] nvarchar(200) NULL,
    [ValorCifrado] nvarchar(4000) NOT NULL,
    [ParametrosCifrados] nvarchar(4000) NULL,
    [FechaRegistro] datetime2 NOT NULL,
    CONSTRAINT [PK_ConnectorCredenciales] PRIMARY KEY ([IdCredencial]),
    CONSTRAINT [FK_ConnectorCredenciales_Connectors_IdConnector] FOREIGN KEY ([IdConnector]) REFERENCES [Connectors] ([IdConnector]) ON DELETE CASCADE
);
CREATE INDEX [IX_ConnectorCredenciales_IdConnector] ON [ConnectorCredenciales] ([IdConnector]);
END
GO

IF OBJECT_ID(N'[dbo].[ConnectorPoliticas]', N'U') IS NULL
BEGIN
CREATE TABLE [ConnectorPoliticas] (
    [IdPolitica] int NOT NULL IDENTITY,
    [IdConnector] int NOT NULL,
    [TimeoutSegundos] int NOT NULL,
    [MaxReintentos] int NOT NULL,
    [IntervaloReintentoMs] int NOT NULL,
    [RateLimitPorMinuto] int NOT NULL,
    [CircuitBreakerUmbralFallos] int NOT NULL,
    [CircuitBreakerSegundosAbierto] int NOT NULL,
    [FechaRegistro] datetime2 NOT NULL,
    CONSTRAINT [PK_ConnectorPoliticas] PRIMARY KEY ([IdPolitica]),
    CONSTRAINT [FK_ConnectorPoliticas_Connectors_IdConnector] FOREIGN KEY ([IdConnector]) REFERENCES [Connectors] ([IdConnector]) ON DELETE CASCADE
);
CREATE INDEX [IX_ConnectorPoliticas_IdConnector] ON [ConnectorPoliticas] ([IdConnector]);
END
GO

IF OBJECT_ID(N'[dbo].[ConnectorEjecuciones]', N'U') IS NULL
BEGIN
CREATE TABLE [ConnectorEjecuciones] (
    [IdEjecucion] int NOT NULL IDENTITY,
    [IdConnector] int NOT NULL,
    [IdUsuario] int NULL,
    [IdAsistente] int NULL,
    [Operacion] nvarchar(200) NOT NULL,
    [Destino] nvarchar(1000) NULL,
    [Estado] nvarchar(30) NOT NULL,
    [CodigoRespuesta] int NULL,
    [LatenciaMs] bigint NOT NULL,
    [Reintentos] int NOT NULL,
    [Error] nvarchar(2000) NULL,
    [RespuestaResumen] nvarchar(2000) NULL,
    [Fecha] datetime2 NOT NULL,
    CONSTRAINT [PK_ConnectorEjecuciones] PRIMARY KEY ([IdEjecucion]),
    CONSTRAINT [FK_ConnectorEjecuciones_Connectors_IdConnector] FOREIGN KEY ([IdConnector]) REFERENCES [Connectors] ([IdConnector]) ON DELETE CASCADE
);
CREATE INDEX [IX_ConnectorEjecuciones_IdConnector_Fecha] ON [ConnectorEjecuciones] ([IdConnector], [Fecha]);
END
GO

-- =====================================================================
-- Herramienta del Gateway para el Planner (si el seed no la creó).
-- =====================================================================
IF NOT EXISTS (SELECT 1 FROM Herramientas WHERE Codigo = 'GatewayConnectorTool')
BEGIN
INSERT INTO Herramientas (Nombre, Codigo, Descripcion, Categoria, Activa, RequierePermiso, FechaRegistro)
VALUES ('Conector via Gateway', 'GatewayConnectorTool',
 'Consume un sistema externo (ERP, CRM, correo, SharePoint, API REST/SOAP) a través del API Gateway Empresarial. Parámetros: conector (código registrado), operacion, recurso (ruta o destinatario), metodo (GET/POST/PUT/DELETE/PATCH), cuerpo.',
 'Integracion', 1, 1, GETUTCDATE());
END
GO

-- =====================================================================
-- Permisos por conector (ejemplo). Cada conector con RequierePermiso=1 exige
-- el permiso CONNECTOR:{Codigo}. Otorgar al rol Administrador (ajustar IdRol
-- según el entorno) después de registrar cada conector:
--
-- DECLARE @idPermiso INT;
-- IF NOT EXISTS (SELECT 1 FROM Permisos WHERE Codigo = 'CONNECTOR:CRM_DEMO')
-- BEGIN
--   INSERT INTO Permisos (Codigo, Nombre, Modulo, Activo)
--   VALUES ('CONNECTOR:CRM_DEMO', 'Usar conector CRM_DEMO', 'Conectores', 1);
-- END
-- SELECT @idPermiso = IdPermiso FROM Permisos WHERE Codigo = 'CONNECTOR:CRM_DEMO';
-- IF NOT EXISTS (SELECT 1 FROM RolPermisos WHERE IdRol = 1 AND IdPermiso = @idPermiso)
--   INSERT INTO RolPermisos (IdRol, IdPermiso) VALUES (1, @idPermiso);
-- =====================================================================

-- =====================================================================
-- Verificación de auditoría y métricas por conector:
-- SELECT Estado, COUNT(*) FROM ConnectorEjecuciones WHERE IdConnector = @id GROUP BY Estado;
-- =====================================================================
