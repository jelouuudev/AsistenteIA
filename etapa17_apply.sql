IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('ConfiguracionOrchestrator') AND name = 'EstrategiaError')
BEGIN
    ALTER TABLE ConfiguracionOrchestrator ADD EstrategiaError nvarchar(max) NOT NULL CONSTRAINT DF_CO_EstrategiaError DEFAULT '';
END
GO
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('ConfiguracionOrchestrator') AND name = 'MaxAgentesPorSolicitud')
    ALTER TABLE ConfiguracionOrchestrator ADD MaxAgentesPorSolicitud int NOT NULL DEFAULT 0;
GO
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('ConfiguracionOrchestrator') AND name = 'MaxHerramientasPorAgente')
    ALTER TABLE ConfiguracionOrchestrator ADD MaxHerramientasPorAgente int NOT NULL DEFAULT 0;
GO
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('ConfiguracionOrchestrator') AND name = 'MaxProfundidad')
    ALTER TABLE ConfiguracionOrchestrator ADD MaxProfundidad int NOT NULL DEFAULT 0;
GO
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('ConfiguracionOrchestrator') AND name = 'MaxTiempoTotalMs')
    ALTER TABLE ConfiguracionOrchestrator ADD MaxTiempoTotalMs int NOT NULL DEFAULT 0;
GO
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('ConfiguracionOrchestrator') AND name = 'ReintentosNodo')
    ALTER TABLE ConfiguracionOrchestrator ADD ReintentosNodo int NOT NULL DEFAULT 0;
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'AgentCollaborationRule')
CREATE TABLE AgentCollaborationRule (
    IdRule int IDENTITY(1,1) NOT NULL,
    AgenteOrigen int NOT NULL,
    AgenteDestino int NOT NULL,
    Permitido bit NOT NULL,
    Prioridad int NOT NULL,
    Activa bit NOT NULL,
    FechaCreacion datetime2 NOT NULL,
    UsuarioCreacion nvarchar(max) NULL,
    CONSTRAINT PK_AgentCollaborationRule PRIMARY KEY (IdRule),
    CONSTRAINT FK_AgentCollaborationRule_Asistente_AgenteDestino FOREIGN KEY (AgenteDestino) REFERENCES Asistente (IdAsistente) ON DELETE NO ACTION,
    CONSTRAINT FK_AgentCollaborationRule_Asistente_AgenteOrigen FOREIGN KEY (AgenteOrigen) REFERENCES Asistente (IdAsistente) ON DELETE NO ACTION
);
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'AgentExecution')
CREATE TABLE AgentExecution (
    IdExecution int IDENTITY(1,1) NOT NULL,
    IdUsuario int NOT NULL,
    IdAgentePrincipal int NOT NULL,
    Pregunta nvarchar(max) NOT NULL,
    FechaInicio datetime2 NOT NULL,
    FechaFin datetime2 NULL,
    Estado nvarchar(20) NOT NULL,
    TiempoTotalMs bigint NULL,
    CantidadAgentes int NOT NULL,
    ProfundidadAlcanzada int NOT NULL,
    HerramientasUtilizadas int NOT NULL,
    RespuestaFinal nvarchar(max) NULL,
    Error nvarchar(max) NULL,
    CONSTRAINT PK_AgentExecution PRIMARY KEY (IdExecution),
    CONSTRAINT FK_AgentExecution_Asistente_IdAgentePrincipal FOREIGN KEY (IdAgentePrincipal) REFERENCES Asistente (IdAsistente) ON DELETE NO ACTION
);
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'AgentExecutionStep')
CREATE TABLE AgentExecutionStep (
    IdStep int IDENTITY(1,1) NOT NULL,
    IdExecution int NOT NULL,
    Orden int NOT NULL,
    IdAgente int NOT NULL,
    Accion nvarchar(max) NOT NULL,
    Resultado nvarchar(max) NULL,
    TiempoMs bigint NOT NULL,
    Estado nvarchar(20) NOT NULL,
    Error nvarchar(max) NULL,
    Dependencias nvarchar(max) NULL,
    CONSTRAINT PK_AgentExecutionStep PRIMARY KEY (IdStep),
    CONSTRAINT FK_AgentExecutionStep_AgentExecution_IdExecution FOREIGN KEY (IdExecution) REFERENCES AgentExecution (IdExecution) ON DELETE CASCADE,
    CONSTRAINT FK_AgentExecutionStep_Asistente_IdAgente FOREIGN KEY (IdAgente) REFERENCES Asistente (IdAsistente) ON DELETE NO ACTION
);
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'AgentExecutionTrace')
CREATE TABLE AgentExecutionTrace (
    IdTrace int IDENTITY(1,1) NOT NULL,
    IdExecution int NOT NULL,
    Evento nvarchar(max) NOT NULL,
    Detalle nvarchar(max) NULL,
    FechaHora datetime2 NOT NULL,
    CONSTRAINT PK_AgentExecutionTrace PRIMARY KEY (IdTrace),
    CONSTRAINT FK_AgentExecutionTrace_AgentExecution_IdExecution FOREIGN KEY (IdExecution) REFERENCES AgentExecution (IdExecution) ON DELETE CASCADE
);
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_AgentCollaborationRule_AgenteDestino')
    CREATE INDEX IX_AgentCollaborationRule_AgenteDestino ON AgentCollaborationRule (AgenteDestino);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_AgentCollaborationRule_AgenteOrigen_AgenteDestino')
    CREATE INDEX IX_AgentCollaborationRule_AgenteOrigen_AgenteDestino ON AgentCollaborationRule (AgenteOrigen, AgenteDestino);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_AgentExecution_IdAgentePrincipal')
    CREATE INDEX IX_AgentExecution_IdAgentePrincipal ON AgentExecution (IdAgentePrincipal);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_AgentExecutionStep_IdAgente')
    CREATE INDEX IX_AgentExecutionStep_IdAgente ON AgentExecutionStep (IdAgente);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_AgentExecutionStep_IdExecution')
    CREATE INDEX IX_AgentExecutionStep_IdExecution ON AgentExecutionStep (IdExecution);
GO
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_AgentExecutionTrace_IdExecution')
    CREATE INDEX IX_AgentExecutionTrace_IdExecution ON AgentExecutionTrace (IdExecution);
GO

-- Registrar la migración en el historial de EF para que 'dotnet ef' la considere aplicada
IF NOT EXISTS (SELECT * FROM __EFMigrationsHistory WHERE MigrationId = '20260822151847_Etapa17AgentOrchestrator')
    INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion) VALUES ('20260822151847_Etapa17AgentOrchestrator', '8.0.0');
GO
