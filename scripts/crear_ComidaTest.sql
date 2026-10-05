-- =============================================
-- Script: Crear base de datos de prueba ComidaTest
-- Proposito: BD de rubro comida (restaurante) para probar que el
-- Planner + SqlQueryTool funcionan con cualquier dominio y cualquier
-- tabla (sin keywords): Platos, Pedidos e Insumos.
-- Servidor: asistentesql,1433
-- =============================================

USE master;
GO

-- Eliminar si ya existe
IF EXISTS (SELECT name FROM sys.databases WHERE name = 'ComidaTest')
BEGIN
    ALTER DATABASE ComidaTest SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE ComidaTest;
END
GO

-- Crear la base de datos
CREATE DATABASE ComidaTest;
GO

USE ComidaTest;
GO

-- =============================================
-- Tabla: Platos
-- =============================================
CREATE TABLE dbo.Platos (
    IdPlato INT IDENTITY(1,1) PRIMARY KEY,
    Nombre NVARCHAR(200) NOT NULL,
    Descripcion NVARCHAR(500) NULL,
    Categoria NVARCHAR(100) NOT NULL,
    Precio DECIMAL(18,2) NOT NULL,
    Estado NVARCHAR(20) NOT NULL DEFAULT 'Disponible',
    TiempoPreparacionMin INT NULL,
    FechaCreacion DATETIME2 NOT NULL DEFAULT GETUTCDATE()
);
GO

-- =============================================
-- Tabla: Pedidos
-- =============================================
CREATE TABLE dbo.Pedidos (
    IdPedido INT IDENTITY(1,1) PRIMARY KEY,
    Cliente NVARCHAR(200) NOT NULL,
    Distrito NVARCHAR(100) NOT NULL,
    Estado NVARCHAR(20) NOT NULL DEFAULT 'Pendiente',
    Cantidad INT NOT NULL DEFAULT 1,
    Total DECIMAL(18,2) NOT NULL,
    FechaPedido DATE NOT NULL,
    Repartidor NVARCHAR(100) NULL,
    CanalPedido NVARCHAR(100) NULL,
    FechaCreacion DATETIME2 NOT NULL DEFAULT GETUTCDATE()
);
GO

-- =============================================
-- Tabla: Insumos
-- =============================================
CREATE TABLE dbo.Insumos (
    IdInsumo INT IDENTITY(1,1) PRIMARY KEY,
    Nombre NVARCHAR(200) NOT NULL,
    Categoria NVARCHAR(100) NOT NULL,
    Unidad NVARCHAR(50) NOT NULL,
    Stock INT NOT NULL DEFAULT 0,
    StockMinimo INT NOT NULL DEFAULT 0,
    CostoUnitario DECIMAL(18,2) NOT NULL,
    Proveedor NVARCHAR(200) NULL,
    FechaIngreso DATE NOT NULL,
    FechaCreacion DATETIME2 NOT NULL DEFAULT GETUTCDATE()
);
GO

-- =============================================
-- Datos de prueba: Platos
-- =============================================
INSERT INTO dbo.Platos (Nombre, Descripcion, Categoria, Precio, Estado, TiempoPreparacionMin)
VALUES
('Lomo Saltado', 'Salteado de carne con papas fritas y arroz', 'Criolla', 38.00, 'Disponible', 20),
('Aji de Gallina', 'Crema de aji amarillo con pollo deshilachado', 'Criolla', 32.00, 'Disponible', 25),
('Tacu Tacu con Lomo', 'Tacu tacu de frejoles con lomo al jugo', 'Criolla', 42.00, 'Agotado', 30),
('Ceviche Clasico', 'Pescado del dia con limon y choclo', 'Marina', 45.00, 'Disponible', 15),
('Jalea Mixta', 'Mixtura frita de mariscos con yuca', 'Marina', 48.00, 'Disponible', 25),
('Sudado de Pescado', 'Pescado sudado con chicha de jora', 'Marina', 40.00, 'Agotado', 30),
('Tiradito Nikkei', 'Laminas de pescado con salsa nikkei', 'Nikkei', 52.00, 'Disponible', 15),
('Ramen Fusion', 'Caldo oriental con fideos y cerdo', 'Nikkei', 36.00, 'Disponible', 20),
('Suspiro Limena', 'Postre de manjar con merengue', 'Postres', 18.00, 'Disponible', 10),
('Picarones', 'Rosquillas de zapallo con miel', 'Postres', 15.00, 'Agotado', 15);
GO

-- =============================================
-- Datos de prueba: Pedidos
-- =============================================
INSERT INTO dbo.Pedidos (Cliente, Distrito, Estado, Cantidad, Total, FechaPedido, Repartidor, CanalPedido)
VALUES
('Rosa Quispe', 'Miraflores', 'Entregado', 2, 76.00, '2026-09-28', 'Diego Torres', 'App Delivery'),
('Carlos Huaman', 'Miraflores', 'Entregado', 3, 135.00, '2026-09-29', 'Diego Torres', 'Telefono'),
('Lucia Paredes', 'San Isidro', 'Entregado', 1, 45.00, '2026-09-29', 'Marco Ruiz', 'Presencial'),
('Jorge Salinas', 'Barranco', 'En camino', 2, 90.00, '2026-09-30', 'Marco Ruiz', 'App Delivery'),
('Ana Flores', 'Surco', 'En preparacion', 4, 152.00, '2026-09-30', 'Diego Torres', 'App Delivery'),
('Pedro Castillo', 'Miraflores', 'En preparacion', 1, 38.00, '2026-10-01', 'Marco Ruiz', 'Telefono'),
('Sofia Vargas', 'San Isidro', 'Cancelado', 2, 84.00, '2026-09-27', 'Diego Torres', 'App Delivery'),
('Miguel Torres', 'Barranco', 'Entregado', 5, 190.00, '2026-09-28', 'Marco Ruiz', 'Presencial'),
('Elena Rios', 'Surco', 'Entregado', 2, 96.00, '2026-09-29', 'Diego Torres', 'Telefono'),
('Raul Diaz', 'Miraflores', 'En camino', 3, 114.00, '2026-10-01', 'Marco Ruiz', 'App Delivery'),
('Carmen Vega', 'San Isidro', 'Entregado', 1, 52.00, '2026-09-30', 'Diego Torres', 'Presencial'),
('Luis Ramos', 'Surco', 'Pendiente', 2, 72.00, '2026-10-01', 'Marco Ruiz', 'Telefono');
GO

-- =============================================
-- Datos de prueba: Insumos
-- =============================================
INSERT INTO dbo.Insumos (Nombre, Categoria, Unidad, Stock, StockMinimo, CostoUnitario, Proveedor, FechaIngreso)
VALUES
('Arroz Extra', 'Granos', 'Kilos', 120, 50, 4.50, 'Mercado Mayorista', '2026-09-20'),
('Aji Amarillo', 'Verduras', 'Kilos', 15, 20, 8.00, 'Mercado Mayorista', '2026-09-28'),
('Pescado del Dia', 'Carnes', 'Kilos', 25, 10, 22.00, 'Terminal Pesquero', '2026-10-01'),
('Pollo Entero', 'Carnes', 'Kilos', 40, 15, 11.50, 'Distribuidora Andina', '2026-09-29'),
('Papa Amarilla', 'Verduras', 'Kilos', 60, 30, 3.80, 'Mercado Mayorista', '2026-09-27'),
('Leche Fresca', 'Lacteos', 'Litros', 30, 12, 5.20, 'Distribuidora Andina', '2026-09-30'),
('Queso Andino', 'Lacteos', 'Kilos', 8, 10, 28.00, 'Distribuidora Andina', '2026-09-25'),
('Aceite Vegetal', 'Granos', 'Litros', 45, 20, 9.90, 'Mercado Mayorista', '2026-09-22');
GO
