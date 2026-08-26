-- Script para corregir el rol del usuario supervisor
-- Ejecutar en SQL Server Management Studio

USE AsistenteIA;
GO

-- Verificar si el usuario supervisor existe
SELECT u.IdUsuario, u.UsuarioNombre, u.Activo, r.Nombre as Rol
FROM Usuario u
LEFT JOIN UsuarioRol ur ON u.IdUsuario = ur.IdUsuario
LEFT JOIN Rol r ON ur.IdRol = r.IdRol
WHERE u.UsuarioNombre = 'supervisor';
GO

-- Si el usuario existe pero no tiene rol, asignar el rol Supervisor
DECLARE @SupervisorId INT;
DECLARE @RolSupervisorId INT;

-- Obtener ID del usuario supervisor
SELECT @SupervisorId = IdUsuario FROM Usuario WHERE UsuarioNombre = 'supervisor';

-- Obtener ID del rol Supervisor
SELECT @RolSupervisorId = IdRol FROM Rol WHERE Nombre = 'Supervisor';

-- Si el usuario existe y el rol existe, asignar el rol
IF @SupervisorId IS NOT NULL AND @RolSupervisorId IS NOT NULL
BEGIN
    -- Eliminar roles existentes (si los hay)
    DELETE FROM UsuarioRol WHERE IdUsuario = @SupervisorId;
    
    -- Asignar rol Supervisor
    INSERT INTO UsuarioRol (IdUsuario, IdRol)
    VALUES (@SupervisorId, @RolSupervisorId);
    
    PRINT 'Rol Supervisor asignado correctamente al usuario supervisor.';
END
ELSE
BEGIN
    IF @SupervisorId IS NULL
        PRINT 'ERROR: El usuario supervisor no existe.';
    IF @RolSupervisorId IS NULL
        PRINT 'ERROR: El rol Supervisor no existe.';
END
GO

-- Verificar la corrección
SELECT u.IdUsuario, u.UsuarioNombre, u.Nombres, u.Apellidos, u.Activo, r.Nombre as Rol
FROM Usuario u
INNER JOIN UsuarioRol ur ON u.IdUsuario = ur.IdUsuario
INNER JOIN Rol r ON ur.IdRol = r.IdRol
WHERE u.UsuarioNombre = 'supervisor';
GO
