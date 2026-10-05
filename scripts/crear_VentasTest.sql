-- =============================================
-- Script: Crear base de datos de prueba VentasTest
-- Propósito: BD de dominio distinto a ControlActivos para probar que el
-- Planner + SqlQueryTool funcionan con cualquier tabla/DB (sin keywords).
-- Servidor: asistentesql,1433
-- =============================================

USE master;
GO

-- Eliminar si ya existe
IF EXISTS (SELECT name FROM sys.databases WHERE name = 'VentasTest')
BEGIN
    ALTER DATABASE VentasTest SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE VentasTest;
END
GO

-- Crear la base de datos
CREATE DATABASE VentasTest;
GO

USE VentasTest;
GO

-- =============================================
-- Tabla: Ventas
-- =============================================
CREATE TABLE dbo.Ventas (
    IdVenta INT IDENTITY(1,1) PRIMARY KEY,
    Producto NVARCHAR(200) NOT NULL,
    Categoria NVARCHAR(100) NOT NULL,
    Cliente NVARCHAR(200) NOT NULL,
    Ciudad NVARCHAR(100) NOT NULL,
    Estado NVARCHAR(20) NOT NULL DEFAULT 'Pendiente',
    Cantidad INT NOT NULL DEFAULT 1,
    PrecioUnitario DECIMAL(18,2) NOT NULL,
    FechaVenta DATE NOT NULL,
    Vendedor NVARCHAR(100) NULL,
    CanalVenta NVARCHAR(100) NULL,
    FechaCreacion DATETIME2 NOT NULL DEFAULT GETUTCDATE()
);
GO

-- =============================================
-- Datos de prueba: 24 ventas variadas
-- =============================================
INSERT INTO dbo.Ventas (Producto, Categoria, Cliente, Ciudad, Estado, Cantidad, PrecioUnitario, FechaVenta, Vendedor, CanalVenta)
VALUES
-- Electrónica ENTREGADO (Lima)
('Laptop Gamer Nitro 5', 'Electrónica', 'Juan Pérez', 'Lima', 'Entregado', 2, 3500.00, '2024-01-12', 'Ana Torres', 'Tienda Física'),
('Audífonos Bluetooth X2', 'Electrónica', 'María García', 'Lima', 'Entregado', 5, 180.00, '2024-01-18', 'Ana Torres', 'Tienda Online'),
('Smart TV 55 Pulgadas', 'Electrónica', 'Tiendas Ripley', 'Lima', 'Entregado', 3, 2200.00, '2024-02-05', 'Luis Quispe', 'Tienda Online'),
('Teclado Mecánico RGB', 'Electrónica', 'Carlos Mendoza', 'Arequipa', 'Entregado', 4, 320.00, '2024-02-11', 'Luis Quispe', 'Tienda Física'),
-- Electrónica ENVIADO / PENDIENTE
('Mouse Inalámbrico Pro', 'Electrónica', 'Sofía Huamán', 'Trujillo', 'Enviado', 6, 95.00, '2024-03-02', 'Ana Torres', 'Tienda Online'),
('Tablet Educativa 10', 'Electrónica', 'Colegio San Marcos', 'Cusco', 'Pendiente', 10, 450.00, '2024-03-15', 'Luis Quispe', 'Tienda Online'),
-- Electrónica CANCELADO
('Parlante Portátil Boom', 'Electrónica', 'Pedro López', 'Lima', 'Cancelado', 1, 250.00, '2024-01-25', 'Ana Torres', 'Tienda Física'),
-- Ropa ENTREGADO
('Camisa Algodón Slim', 'Ropa', 'María García', 'Arequipa', 'Entregado', 8, 75.00, '2024-01-20', 'Carmen Ruiz', 'Tienda Física'),
('Jean Clásico Azul', 'Ropa', 'Juan Pérez', 'Lima', 'Entregado', 3, 120.00, '2024-02-02', 'Carmen Ruiz', 'Tienda Online'),
('Casaca de Cuero', 'Ropa', 'Tiendas Ripley', 'Trujillo', 'Entregado', 2, 480.00, '2024-02-14', 'Carmen Ruiz', 'Tienda Física'),
('Zapatillas Running Air', 'Ropa', 'Sofía Huamán', 'Lima', 'Entregado', 4, 350.00, '2024-02-20', 'Carmen Ruiz', 'Tienda Online'),
-- Ropa ENVIADO / PENDIENTE / CANCELADO
('Vestido Floral Verano', 'Ropa', 'Lucía Fernández', 'Cusco', 'Enviado', 5, 140.00, '2024-03-08', 'Carmen Ruiz', 'Tienda Online'),
('Buzo Deportivo Gris', 'Ropa', 'Carlos Mendoza', 'Arequipa', 'Pendiente', 2, 110.00, '2024-03-12', 'Carmen Ruiz', 'Tienda Física'),
('Correa de Cuero Negra', 'Ropa', 'Pedro López', 'Trujillo', 'Cancelado', 1, 45.00, '2024-01-28', 'Carmen Ruiz', 'Tienda Física'),
-- Hogar ENTREGADO
('Juego de Ollas Acero', 'Hogar', 'Tiendas Ripley', 'Lima', 'Entregado', 2, 520.00, '2024-01-15', 'Jorge Paredes', 'Tienda Física'),
('Aspiradora Robot Max', 'Hogar', 'Lucía Fernández', 'Arequipa', 'Entregado', 1, 890.00, '2024-02-08', 'Jorge Paredes', 'Tienda Online'),
('Lámpara de Escritorio LED', 'Hogar', 'Colegio San Marcos', 'Cusco', 'Entregado', 12, 65.00, '2024-02-22', 'Jorge Paredes', 'Tienda Online'),
('Set de Cuchillos Chef', 'Hogar', 'Sofía Huamán', 'Trujillo', 'Entregado', 3, 210.00, '2024-03-01', 'Jorge Paredes', 'Tienda Física'),
-- Hogar ENVIADO / PENDIENTE / CANCELADO
('Cafetera Espresso Bar', 'Hogar', 'Juan Pérez', 'Lima', 'Enviado', 1, 750.00, '2024-03-10', 'Jorge Paredes', 'Tienda Online'),
('Plancha a Vapor Pro', 'Hogar', 'María García', 'Arequipa', 'Pendiente', 2, 130.00, '2024-03-14', 'Jorge Paredes', 'Tienda Física'),
('Tostadora Retro Roja', 'Hogar', 'Pedro López', 'Cusco', 'Cancelado', 1, 95.00, '2024-01-30', 'Jorge Paredes', 'Tienda Online'),
-- Electrónica ENTREGADO extra (Arequipa / Trujillo)
('Cargador Rápido 65W', 'Electrónica', 'Lucía Fernández', 'Arequipa', 'Entregado', 7, 60.00, '2024-02-25', 'Ana Torres', 'Tienda Física'),
('Webcam HD 1080p', 'Electrónica', 'Colegio San Marcos', 'Trujillo', 'Entregado', 6, 150.00, '2024-03-05', 'Ana Torres', 'Tienda Online'),
('Disco Sólido 1TB', 'Electrónica', 'Tiendas Ripley', 'Cusco', 'Entregado', 5, 380.00, '2024-03-11', 'Luis Quispe', 'Tienda Física');
GO

-- =============================================
-- Verificación
-- =============================================
SELECT
    'Total Ventas' AS Metrica,
    COUNT(*) AS Valor
FROM dbo.Ventas
UNION ALL
SELECT
    'Total Unidades',
    SUM(Cantidad)
FROM dbo.Ventas
UNION ALL
SELECT
    'Monto Total',
    CAST(SUM(Cantidad * PrecioUnitario) AS INT)
FROM dbo.Ventas;
GO

SELECT
    Categoria,
    Estado,
    COUNT(*) AS Cantidad,
    SUM(Cantidad * PrecioUnitario) AS MontoTotal
FROM dbo.Ventas
GROUP BY Categoria, Estado
ORDER BY Categoria, Estado;
GO

SELECT Ciudad, COUNT(*) AS Cantidad FROM dbo.Ventas GROUP BY Ciudad ORDER BY Ciudad;
GO

PRINT 'Base de datos VentasTest creada exitosamente con 24 registros de prueba.';
GO
