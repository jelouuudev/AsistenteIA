-- ============================================================================
-- ETAPA 19 — Centro de Aprobaciones (Human-in-the-Loop)
-- Script SQL de creación de tablas + política por defecto.
-- Aplicado vía EF Migration 'Etapa19CentroAprobaciones' (20260827153826).
-- Este archivo es el entregable SQL solicitado en el RF §19 punto 4.
-- ============================================================================

PRINT 'ETAPA 19: creando tablas de aprobaciones...';

IF OBJECT_ID('dbo.ApprovalPolicy', 'U') IS NULL
CREATE TABLE dbo.ApprovalPolicy (
    IdPolicy                   INT IDENTITY(1,1) PRIMARY KEY,
    Nombre                     NVARCHAR(100) NOT NULL,
    CantidadMinimaAprobaciones INT NOT NULL DEFAULT 1,
    RequiereUnanimidad         BIT NOT NULL DEFAULT 0,
    PermiteDelegacion          BIT NOT NULL DEFAULT 1,
    TiempoMaximoHoras          INT NOT NULL DEFAULT 0,
    Activo                     BIT NOT NULL DEFAULT 1
);
GO

IF OBJECT_ID('dbo.ApprovalRequest', 'U') IS NULL
CREATE TABLE dbo.ApprovalRequest (
    IdApproval        INT IDENTITY(1,1) PRIMARY KEY,
    Codigo            NVARCHAR(30) NOT NULL,
    IdPlan            INT NOT NULL,
    Tipo              INT NOT NULL DEFAULT 0,           -- 0 Operacional,1 Financiera,2 Administrativa,3 Seguridad,4 Publicacion,5 Manual
    Estado            INT NOT NULL DEFAULT 0,           -- 0 Pendiente,1 EnRevision,2 Aprobado,3 Rechazado,4 Cancelado,5 Expirado,6 Delegado
    FechaSolicitud    DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    FechaVencimiento  DATETIME2 NULL,
    Solicitante       INT NOT NULL,
    Observaciones     NVARCHAR(MAX) NULL,
    IdPolicy          INT NULL,
    CONSTRAINT FK_ApprovalRequest_Policy FOREIGN KEY (IdPolicy) REFERENCES dbo.ApprovalPolicy(IdPolicy) ON DELETE SET NULL
);
GO

IF OBJECT_ID('dbo.ApprovalAssignee', 'U') IS NULL
CREATE TABLE dbo.ApprovalAssignee (
    IdAssignee   INT IDENTITY(1,1) PRIMARY KEY,
    IdApproval   INT NOT NULL,
    IdUsuario    INT NOT NULL,
    EsPrincipal  BIT NOT NULL DEFAULT 0,
    Estado       NVARCHAR(20) NOT NULL DEFAULT 'Pendiente',
    CONSTRAINT FK_ApprovalAssignee_Request FOREIGN KEY (IdApproval) REFERENCES dbo.ApprovalRequest(IdApproval) ON DELETE CASCADE
);
GO

IF OBJECT_ID('dbo.ApprovalDecision', 'U') IS NULL
CREATE TABLE dbo.ApprovalDecision (
    IdDecision    INT IDENTITY(1,1) PRIMARY KEY,
    IdApproval    INT NOT NULL,
    IdUsuario     INT NOT NULL,
    Decision      NVARCHAR(20) NOT NULL,                  -- Aprobar, Rechazar, Delegar
    Comentario    NVARCHAR(MAX) NULL,
    FechaDecision DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_ApprovalDecision_Request FOREIGN KEY (IdApproval) REFERENCES dbo.ApprovalRequest(IdApproval) ON DELETE CASCADE
);
GO

-- Política por defecto (aprobación simple: 1 aprobación, permite delegación).
IF NOT EXISTS (SELECT 1 FROM dbo.ApprovalPolicy WHERE Nombre = 'PoliticaDefecto')
INSERT INTO dbo.ApprovalPolicy (Nombre, CantidadMinimaAprobaciones, RequiereUnanimidad, PermiteDelegacion, TiempoMaximoHoras, Activo)
VALUES ('PoliticaDefecto', 1, 0, 1, 0, 1);
GO

PRINT 'ETAPA 19: tablas de aprobaciones listas.';
