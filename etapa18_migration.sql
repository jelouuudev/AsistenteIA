BEGIN TRANSACTION;
GO

CREATE TABLE [Plan] (
    [IdPlan] int NOT NULL IDENTITY,
    [IdUsuario] int NOT NULL,
    [Objetivo] nvarchar(max) NOT NULL,
    [Estado] nvarchar(20) NOT NULL DEFAULT N'Borrador',
    [FechaCreacion] datetime2 NOT NULL,
    [FechaInicio] datetime2 NULL,
    [FechaFin] datetime2 NULL,
    [TiempoTotalMs] bigint NULL,
    [Version] int NOT NULL,
    [Razonamiento] nvarchar(max) NULL,
    [RequiereAprobacion] bit NOT NULL,
    [Aprobado] bit NOT NULL,
    [IdExecution] nvarchar(max) NULL,
    CONSTRAINT [PK_Plan] PRIMARY KEY ([IdPlan])
);
GO

CREATE TABLE [PlanDependency] (
    [IdDependency] int NOT NULL IDENTITY,
    [IdPlan] int NOT NULL,
    [StepOrigen] int NOT NULL,
    [StepDestino] int NOT NULL,
    CONSTRAINT [PK_PlanDependency] PRIMARY KEY ([IdDependency]),
    CONSTRAINT [FK_PlanDependency_Plan_IdPlan] FOREIGN KEY ([IdPlan]) REFERENCES [Plan] ([IdPlan]) ON DELETE CASCADE
);
GO

CREATE TABLE [PlanExecutionLog] (
    [IdLog] int NOT NULL IDENTITY,
    [IdPlan] int NOT NULL,
    [IdStep] int NULL,
    [Evento] nvarchar(max) NOT NULL,
    [Detalle] nvarchar(max) NULL,
    [Fecha] datetime2 NOT NULL,
    CONSTRAINT [PK_PlanExecutionLog] PRIMARY KEY ([IdLog]),
    CONSTRAINT [FK_PlanExecutionLog_Plan_IdPlan] FOREIGN KEY ([IdPlan]) REFERENCES [Plan] ([IdPlan]) ON DELETE CASCADE
);
GO

CREATE TABLE [PlanStep] (
    [IdStep] int NOT NULL IDENTITY,
    [IdPlan] int NOT NULL,
    [Orden] int NOT NULL,
    [Tipo] nvarchar(20) NOT NULL,
    [Nombre] nvarchar(max) NOT NULL,
    [Descripcion] nvarchar(max) NULL,
    [Estado] nvarchar(20) NOT NULL DEFAULT N'Pendiente',
    [Resultado] nvarchar(max) NULL,
    [IdAsistente] int NULL,
    [CodigoHerramienta] nvarchar(max) NULL,
    [IdWorkflow] int NULL,
    [Intentos] int NOT NULL,
    CONSTRAINT [PK_PlanStep] PRIMARY KEY ([IdStep]),
    CONSTRAINT [FK_PlanStep_Plan_IdPlan] FOREIGN KEY ([IdPlan]) REFERENCES [Plan] ([IdPlan]) ON DELETE CASCADE
);
GO

CREATE INDEX [IX_PlanDependency_IdPlan] ON [PlanDependency] ([IdPlan]);
GO

CREATE INDEX [IX_PlanExecutionLog_IdPlan] ON [PlanExecutionLog] ([IdPlan]);
GO

CREATE INDEX [IX_PlanStep_IdPlan] ON [PlanStep] ([IdPlan]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260825002802_Etapa18PlannerEngine', N'8.0.0');
GO

COMMIT;
GO

