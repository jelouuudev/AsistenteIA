-- Crear la base de datos AsistenteIA
IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'AsistenteIA')
BEGIN
    CREATE DATABASE AsistenteIA;
END
GO

USE AsistenteIA;
GO
