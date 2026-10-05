-- =============================================
-- Script: Crear base de datos de prueba ControlActivosTest
-- Propósito: Crear una BD similar a ControlActivos para pruebas del Planner Engine
-- Servidor: asistentesql,1433
-- =============================================

USE master;
GO

-- Eliminar si ya existe
IF EXISTS (SELECT name FROM sys.databases WHERE name = 'ControlActivosTest')
BEGIN
    ALTER DATABASE ControlActivosTest SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE ControlActivosTest;
END
GO

-- Crear la base de datos
CREATE DATABASE ControlActivosTest;
GO

USE ControlActivosTest;
GO

-- =============================================
-- Tabla: Activos
-- Similar a la tabla Activos de ControlActivos
-- =============================================
CREATE TABLE dbo.Activos (
    IdActivo INT IDENTITY(1,1) PRIMARY KEY,
    Nombre NVARCHAR(200) NOT NULL,
    Descripcion NVARCHAR(500) NULL,
    Categoria NVARCHAR(100) NOT NULL,
    Estado NVARCHAR(20) NOT NULL DEFAULT 'ACTIVO',
    Precio DECIMAL(18,2) NOT NULL,
    FechaAdquisicion DATE NOT NULL,
    Ubicacion NVARCHAR(200) NULL,
    Responsable NVARCHAR(100) NULL,
    NumeroSerie NVARCHAR(50) NULL,
    Marca NVARCHAR(100) NULL,
    Modelo NVARCHAR(100) NULL,
    FechaCreacion DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    FechaModificacion DATETIME2 NULL
);
GO

-- =============================================
-- Datos de prueba: 25 activos variados
-- =============================================
INSERT INTO dbo.Activos (Nombre, Descripcion, Categoria, Estado, Precio, FechaAdquisicion, Ubicacion, Responsable, NumeroSerie, Marca, Modelo)
VALUES
-- Equipos de cómputo (ACTIVOS)
('Laptop Dell Latitude 5540', 'Laptop corporativa para desarrollo', 'Equipos de Cómputo', 'ACTIVO', 4500.00, '2024-01-15', 'Oficina Lima - Piso 3', 'Carlos Mendoza', 'DL-LAT-5540-001', 'Dell', 'Latitude 5540'),
('Laptop HP EliteBook 840', 'Laptop para equipo de ventas', 'Equipos de Cómputo', 'ACTIVO', 3800.00, '2024-02-20', 'Oficina Lima - Piso 2', 'María García', 'HP-EB-840-002', 'HP', 'EliteBook 840 G10'),
('Monitor Samsung 27"', 'Monitor 4K para diseño gráfico', 'Equipos de Cómputo', 'ACTIVO', 1200.00, '2024-03-10', 'Oficina Lima - Piso 3', 'Carlos Mendoza', 'SM-MON-27-003', 'Samsung', 'U28R550'),
('Impresora HP LaserJet', 'Impresora láser monocromática', 'Equipos de Cómputo', 'ACTIVO', 850.00, '2023-11-05', 'Oficina Lima - Piso 1', 'Juan Pérez', 'HP-LJ-P401-004', 'HP', 'LaserJet Pro M404'),
('Servidor Dell PowerEdge', 'Servidor para base de datos', 'Equipos de Cómputo', 'ACTIVO', 15000.00, '2023-06-15', 'Data Center - Sala A', 'Roberto Silva', 'DL-PR-R750-005', 'Dell', 'PowerEdge R750'),

-- Mobiliario (ACTIVOS)
('Mesa de oficina 1.6m', 'Mesa melamina para workstation', 'Mobiliario', 'ACTIVO', 450.00, '2024-01-10', 'Oficina Lima - Piso 2', 'María García', 'MOB-MESA-160-006', 'Genérica', 'Mesa 1.6m'),
('Silla ergonómica', 'Silla con soporte lumbar', 'Mobiliario', 'ACTIVO', 380.00, '2024-01-10', 'Oficina Lima - Piso 2', 'María García', 'MOB-SILLA-007', 'Herman Miller', 'Aeron'),
('Archivador 4 gavetas', 'Archivador metálico', 'Mobiliario', 'ACTIVO', 220.00, '2023-08-20', 'Oficina Lima - Piso 1', 'Juan Pérez', 'MOB-ARCH-008', 'Genérica', '4 Gavetas'),
('Estantería metálica', 'Estantería para almacén', 'Mobiliario', 'ACTIVO', 350.00, '2023-09-15', 'Almacén Central', 'Pedro López', 'MOB-EST-009', 'Genérica', 'Estantería 5 niveles'),

-- Vehículos (ACTIVOS)
('Toyota Hilux 2023', 'Camioneta para logística', 'Vehículos', 'ACTIVO', 125000.00, '2023-03-01', 'Garaje Central', 'Pedro López', 'VEH-HIL-2023-010', 'Toyota', 'Hilux 4x4'),
('Nissan NV350 2022', 'Furgón para reparto', 'Vehículos', 'ACTIVO', 85000.00, '2022-08-15', 'Garaje Central', 'Pedro López', 'VEH-NV3-2022-011', 'Nissan', 'NV350 Urvan'),

-- Maquinaria (ACTIVOS)
('Torno CNC Haas', 'Torno control numérico para producción', 'Maquinaria', 'ACTIVO', 185000.00, '2022-05-10', 'Planta Industrial - Zona B', 'Roberto Silva', 'MAQ-TOR-HAAS-012', 'Haas', 'ST-20Y'),
('Fresadora vertical', 'Fresadora para mecanizado de piezas', 'Maquinaria', 'ACTIVO', 95000.00, '2021-11-20', 'Planta Industrial - Zona A', 'Roberto Silva', 'MAQ-FRES-013', 'Bridgeport', 'Series I'),
('Compresor de aire', 'Compresor industrial 5HP', 'Maquinaria', 'ACTIVO', 12000.00, '2023-02-28', 'Planta Industrial - Zona C', 'Roberto Silva', 'MAQ-COMP-014', 'Atlas Copco', 'GA 5 VSD'),

-- Equipos de red (ACTIVOS)
('Switch Cisco 48 puertos', 'Switch de red para oficina', 'Equipos de Red', 'ACTIVO', 3200.00, '2024-01-05', 'Data Center - Sala A', 'Roberto Silva', 'RED-SW-CISCO-015', 'Cisco', 'CBS350-48P'),
('Router Fortinet', 'Firewall y router principal', 'Equipos de Red', 'ACTIVO', 5800.00, '2023-12-01', 'Data Center - Sala A', 'Roberto Silva', 'RED-RT-FORT-016', 'Fortinet', 'FortiGate 60F'),
('Access Point Ubiquiti', 'Punto de acceso WiFi 6', 'Equipos de Red', 'ACTIVO', 450.00, '2024-02-15', 'Oficina Lima - Piso 3', 'Carlos Mendoza', 'RED-AP-UBI-017', 'Ubiquiti', 'UniFi 6 Pro'),

-- Equipos de cómputo (INACTIVOS - dados de baja)
('Laptop Lenovo ThinkPad T480', 'Laptop dada de baja por obsolescencia', 'Equipos de Cómputo', 'INACTIVO', 2800.00, '2019-03-10', 'Almacén Central - Baja', 'Carlos Mendoza', 'LEN-TP-T480-018', 'Lenovo', 'ThinkPad T480'),
('Monitor LG 24"', 'Monitor dañado dado de baja', 'Equipos de Cómputo', 'INACTIVO', 350.00, '2020-06-15', 'Almacén Central - Baja', 'Juan Pérez', 'LG-MON-24-019', 'LG', '24MK430'),
('Impresora Epson L3150', 'Impresora fuera de servicio', 'Equipos de Cómputo', 'INACTIVO', 450.00, '2020-01-20', 'Almacén Central - Baja', 'Juan Pérez', 'EPS-L3150-020', 'Epson', 'L3150'),

-- Mobiliario (INACTIVOS)
('Mesa de reuniones 3m', 'Mesa de madera dada de baja', 'Mobiliario', 'INACTIVO', 1200.00, '2018-05-10', 'Almacén Central - Baja', 'María García', 'MOB-MESA-300-021', 'Genérica', 'Mesa 3m'),
('Silla de oficina básica', 'Silla rota dada de baja', 'Mobiliario', 'INACTIVO', 150.00, '2019-09-15', 'Almacén Central - Baja', 'María García', 'MOB-SILLA-B022', 'Genérica', 'Básica'),

-- Vehículos (INACTIVOS)
('Toyota Corolla 2018', 'Sedán dado de baja por venta', 'Vehículos', 'INACTIVO', 45000.00, '2018-07-01', 'Garaje Central - Vendido', 'Pedro López', 'VEH-COR-2018-023', 'Toyota', 'Corolla'),
('Moto Honda XR150', 'Moto para reparto dada de baja', 'Vehículos', 'INACTIVO', 8500.00, '2019-11-20', 'Garaje Central - Baja', 'Pedro López', 'VEH-XR150-024', 'Honda', 'XR150'),

-- Maquinaria (INACTIVOS)
('Torno convencional', 'Torno viejo dado de baja', 'Maquinaria', 'INACTIVO', 35000.00, '2015-03-10', 'Planta Industrial - Chatarra', 'Roberto Silva', 'MAQ-TOR-VIEJO-025', 'Genérico', 'Torno 1m');
GO

-- =============================================
-- Verificación
-- =============================================
SELECT 
    'Total Activos' AS Metrica,
    COUNT(*) AS Valor
FROM dbo.Activos
UNION ALL
SELECT 
    'Activos ACTIVOS',
    COUNT(*)
FROM dbo.Activos
WHERE Estado = 'ACTIVO'
UNION ALL
SELECT 
    'Activos INACTIVOS',
    COUNT(*)
FROM dbo.Activos
WHERE Estado = 'INACTIVO'
UNION ALL
SELECT 
    'Valor Total Activos',
    CAST(SUM(Precio) AS INT)
FROM dbo.Activos;
GO

SELECT 
    Categoria,
    Estado,
    COUNT(*) AS Cantidad,
    SUM(Precio) AS ValorTotal
FROM dbo.Activos
GROUP BY Categoria, Estado
ORDER BY Categoria, Estado;
GO

PRINT 'Base de datos ControlActivosTest creada exitosamente con 25 registros de prueba.';
GO
