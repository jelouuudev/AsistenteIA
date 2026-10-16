-- =====================================================================
-- ETAPA 21: Memoria Empresarial Avanzada y Contexto Persistente
-- Tablas: MemoryScopes, MemoryCategories, MemoryRecords, MemoryVersions,
--         MemoryAccessLogs, MemoryRetentionPolicies
-- Equivalente a la migración EF 20261016192129_Etapa21_MemoriaEmpresarial.
-- Idempotente: solo crea lo que no existe.
-- =====================================================================

IF OBJECT_ID(N'[dbo].[MemoryAccessLogs]', N'U') IS NULL
BEGIN
CREATE TABLE [MemoryAccessLogs] (

    [IdLog] int NOT NULL IDENTITY,
    [IdMemory] int NULL,
    [Codigo] nvarchar(100) NOT NULL,
    [IdUsuario] int NULL,
    [IdAsistente] int NULL,
    [Operacion] nvarchar(30) NOT NULL,
    [Resultado] nvarchar(30) NOT NULL,
    [Motivo] nvarchar(1000) NULL,
    [Fecha] datetime2 NOT NULL,
    CONSTRAINT [PK_MemoryAccessLogs] PRIMARY KEY ([IdLog])
);
END
GO

IF OBJECT_ID(N'[dbo].[MemoryCategories]', N'U') IS NULL
BEGIN
CREATE TABLE [MemoryCategories] (

    [IdCategory] int NOT NULL IDENTITY,
    [Codigo] nvarchar(50) NOT NULL,
    [Nombre] nvarchar(150) NOT NULL,
    [Activo] bit NOT NULL,
    CONSTRAINT [PK_MemoryCategories] PRIMARY KEY ([IdCategory])
);
END
GO

IF OBJECT_ID(N'[dbo].[MemoryScopes]', N'U') IS NULL
BEGIN
CREATE TABLE [MemoryScopes] (

    [IdScope] int NOT NULL IDENTITY,
    [Codigo] nvarchar(30) NOT NULL,
    [Nombre] nvarchar(100) NOT NULL,
    [Descripcion] nvarchar(500) NULL,
    [Activo] bit NOT NULL,
    CONSTRAINT [PK_MemoryScopes] PRIMARY KEY ([IdScope])
);
END
GO

IF OBJECT_ID(N'[dbo].[MemoryRecords]', N'U') IS NULL
BEGIN
CREATE TABLE [MemoryRecords] (

    [IdMemory] int NOT NULL IDENTITY,
    [Codigo] nvarchar(100) NOT NULL,
    [Titulo] nvarchar(200) NOT NULL,
    [Contenido] nvarchar(max) NOT NULL,
    [IdScope] int NOT NULL,
    [IdPropietario] int NULL,
    [IdCategory] int NULL,
    [Activo] bit NOT NULL,
    [VersionActual] int NOT NULL,
    [RequierePermiso] bit NOT NULL,
    [VectorId] uniqueidentifier NULL,
    [Indexada] bit NOT NULL,
    [FechaExpiracion] datetime2 NULL,
    [FechaCreacion] datetime2 NOT NULL,
    [FechaActualizacion] datetime2 NOT NULL,
    CONSTRAINT [PK_MemoryRecords] PRIMARY KEY ([IdMemory]),
    CONSTRAINT [FK_MemoryRecords_MemoryCategories_IdCategory] FOREIGN KEY ([IdCategory]) REFERENCES [MemoryCategories] ([IdCategory]) ON DELETE SET NULL,
    CONSTRAINT [FK_MemoryRecords_MemoryScopes_IdScope] FOREIGN KEY ([IdScope]) REFERENCES [MemoryScopes] ([IdScope]) ON DELETE NO ACTION
);
END
GO

IF OBJECT_ID(N'[dbo].[MemoryRetentionPolicies]', N'U') IS NULL
BEGIN
CREATE TABLE [MemoryRetentionPolicies] (

    [IdPolicy] int NOT NULL IDENTITY,
    [IdScope] int NULL,
    [DiasRetencion] int NOT NULL,
    [EliminarAlExpirar] bit NOT NULL,
    [ConservarTrazabilidad] bit NOT NULL,
    [Activo] bit NOT NULL,
    CONSTRAINT [PK_MemoryRetentionPolicies] PRIMARY KEY ([IdPolicy]),
    CONSTRAINT [FK_MemoryRetentionPolicies_MemoryScopes_IdScope] FOREIGN KEY ([IdScope]) REFERENCES [MemoryScopes] ([IdScope]) ON DELETE SET NULL
);
END
GO

IF OBJECT_ID(N'[dbo].[MemoryVersions]', N'U') IS NULL
BEGIN
CREATE TABLE [MemoryVersions] (

    [IdVersion] int NOT NULL IDENTITY,
    [IdMemory] int NOT NULL,
    [Numero] int NOT NULL,
    [Contenido] nvarchar(max) NOT NULL,
    [Motivo] nvarchar(500) NULL,
    [IdUsuario] int NULL,
    [Fecha] datetime2 NOT NULL,
    CONSTRAINT [PK_MemoryVersions] PRIMARY KEY ([IdVersion]),
    CONSTRAINT [FK_MemoryVersions_MemoryRecords_IdMemory] FOREIGN KEY ([IdMemory]) REFERENCES [MemoryRecords] ([IdMemory]) ON DELETE CASCADE
);
END
GO

GO
-- Índices (idempotentes)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = ''IX_MemoryAccessLogs_Codigo_Fecha'')
  CREATE INDEX [IX_MemoryAccessLogs_Codigo_Fecha] ON [MemoryAccessLogs] ([Codigo], [Fecha]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = ''IX_MemoryAccessLogs_IdMemory_Fecha'')
  CREATE INDEX [IX_MemoryAccessLogs_IdMemory_Fecha] ON [MemoryAccessLogs] ([IdMemory], [Fecha]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = ''IX_MemoryCategories_Codigo'')
  CREATE UNIQUE INDEX [IX_MemoryCategories_Codigo] ON [MemoryCategories] ([Codigo]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = ''IX_MemoryRecords_Codigo'')
  CREATE UNIQUE INDEX [IX_MemoryRecords_Codigo] ON [MemoryRecords] ([Codigo]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = ''IX_MemoryRecords_IdScope_Activo'')
  CREATE INDEX [IX_MemoryRecords_IdScope_Activo] ON [MemoryRecords] ([IdScope], [Activo]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = ''IX_MemoryRetentionPolicies_IdScope'')
  CREATE INDEX [IX_MemoryRetentionPolicies_IdScope] ON [MemoryRetentionPolicies] ([IdScope]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = ''IX_MemoryScopes_Codigo'')
  CREATE UNIQUE INDEX [IX_MemoryScopes_Codigo] ON [MemoryScopes] ([Codigo]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = ''IX_MemoryVersions_IdMemory_Numero'')
  CREATE UNIQUE INDEX [IX_MemoryVersions_IdMemory_Numero] ON [MemoryVersions] ([IdMemory], [Numero]);
GO-- Catálogos y herramienta (idempotente). Equivale al seed de DbInitializer.
-- =====================================================================
IF NOT EXISTS (SELECT 1 FROM Herramientas WHERE Codigo = ''MemoryTool'')
BEGIN
INSERT INTO Herramientas (Nombre, Codigo, Descripcion, Categoria, Activa, RequierePermiso, FechaRegistro)
VALUES (''Memoria institucional'', ''MemoryTool'',
 ''Consulta y guarda memoria institucional (formatos de reportes, definiciones, preferencias, contexto de workflows). Parámetros: operacion (buscar|leer|guardar), texto, codigo, titulo, contenido, ambito (Usuario|Agente|Organizacion|Workflow).'',
 ''Memoria'', 1, 1, GETUTCDATE());
END
GO
MERGE MemoryScopes AS t USING (VALUES
 (''Conversacional'',''Conversacional'',''Contexto de conversaciones''),
 (''Usuario'',''Usuario'',''Preferencias y datos del usuario''),
 (''Agente'',''Agente'',''Conocimiento propio del agente''),
 (''Organizacion'',''Organización'',''Conocimiento institucional compartido''),
 (''Workflow'',''Workflow'',''Estado persistente de workflows'')
) AS s(Codigo,Nombre,Descripcion)
ON t.Codigo = s.Codigo
WHEN NOT MATCHED THEN INSERT (Codigo,Nombre,Descripcion,Activo) VALUES (s.Codigo,s.Nombre,s.Descripcion,1);
GO
MERGE MemoryCategories AS t USING (VALUES
 (''FormatoReporte'',''Formato de reporte''),
 (''Definicion'',''Definición institucional''),
 (''Preferencia'',''Preferencia de usuario''),
 (''Hecho'',''Hecho operativo''),
 (''Procedimiento'',''Procedimiento'')
) AS s(Codigo,Nombre)
ON t.Codigo = s.Codigo
WHEN NOT MATCHED THEN INSERT (Codigo,Nombre,Activo) VALUES (s.Codigo,s.Nombre,1);
GO
IF NOT EXISTS (SELECT 1 FROM MemoryRetentionPolicies WHERE IdScope IS NULL)
  INSERT INTO MemoryRetentionPolicies (IdScope,DiasRetencion,EliminarAlExpirar,ConservarTrazabilidad,Activo)
  VALUES (NULL,365,0,1,1);
GO