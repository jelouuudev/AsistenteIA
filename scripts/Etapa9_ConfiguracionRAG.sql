IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ConfiguracionesRAG')
BEGIN
    CREATE TABLE ConfiguracionesRAG (
        IdConfiguracion INT IDENTITY(1,1) PRIMARY KEY,
        MaxChunks INT NOT NULL DEFAULT 5,
        MaxCaracteresContexto INT NOT NULL DEFAULT 8000,
        MinScore FLOAT NOT NULL DEFAULT 0.45,
        TopKPorFuente INT NOT NULL DEFAULT 5,
        MaxFuentesConsultadas INT NOT NULL DEFAULT 5,
        MaxReferencias INT NOT NULL DEFAULT 10,
        MaxChunksAlModelo INT NOT NULL DEFAULT 10,
        UsarDocumentosHistoricos BIT NOT NULL DEFAULT 0,
        Activo BIT NOT NULL DEFAULT 1
    );

    INSERT INTO ConfiguracionesRAG (MaxChunks, MaxCaracteresContexto, MinScore, TopKPorFuente, MaxFuentesConsultadas, MaxReferencias, MaxChunksAlModelo, UsarDocumentosHistoricos, Activo)
    VALUES (5, 8000, 0.45, 5, 5, 10, 10, 0, 1);
END
