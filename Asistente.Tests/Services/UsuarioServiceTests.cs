using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Asistente.Application.Interfaces;
using Asistente.Application.Services;
using Asistente.Domain.Entities;
using Asistente.Domain.Interfaces;
using Asistente.Shared;
using Moq;

namespace Asistente.Tests.Services;

public class UsuarioServiceTests
{
    private readonly Mock<IUsuarioRepository> _usuarioRepoMock = new();
    private readonly Mock<IRolRepository> _rolRepoMock = new();
    private readonly Mock<IAuditoriaRepository> _auditoriaRepoMock = new();
    private readonly Mock<IPasswordHasher> _passwordHasherMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<IJwtTokenService> _jwtTokenServiceMock = new();

    private UsuarioService CreateService() =>
        new(_usuarioRepoMock.Object, _rolRepoMock.Object, _auditoriaRepoMock.Object,
            _passwordHasherMock.Object, _unitOfWorkMock.Object, _jwtTokenServiceMock.Object);

    [Fact]
    public async Task Login_UserNotFound_ReturnsFailure()
    {
        _usuarioRepoMock.Setup(r => r.GetByUsernameAsync("unknown")).ReturnsAsync((Usuario?)null);

        var service = CreateService();
        var result = await service.LoginAsync(new LoginRequest { Usuario = "unknown", Contrasena = "pass" });

        Assert.False(result.Exitoso);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public async Task Login_InactiveUser_ReturnsFailure()
    {
        var user = new Usuario { IdUsuario = 1, UsuarioNombre = "user1", Activo = false, PasswordHash = "hash" };
        _usuarioRepoMock.Setup(r => r.GetByUsernameAsync("user1")).ReturnsAsync(user);
        _auditoriaRepoMock.Setup(r => r.AddSesionAsync(It.IsAny<AuditoriaSesion>())).Returns(Task.CompletedTask);
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(default)).ReturnsAsync(1);

        var service = CreateService();
        var result = await service.LoginAsync(new LoginRequest { Usuario = "user1", Contrasena = "pass" });

        Assert.False(result.Exitoso);
        Assert.Contains("deshabilitado", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Login_WrongPassword_ReturnsFailure()
    {
        var user = new Usuario { IdUsuario = 1, UsuarioNombre = "user1", Activo = true, PasswordHash = "correcthash" };
        _usuarioRepoMock.Setup(r => r.GetByUsernameAsync("user1")).ReturnsAsync(user);
        _passwordHasherMock.Setup(h => h.VerifyPassword("wrongpass", "correcthash")).Returns(false);
        _auditoriaRepoMock.Setup(r => r.AddSesionAsync(It.IsAny<AuditoriaSesion>())).Returns(Task.CompletedTask);
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(default)).ReturnsAsync(1);

        var service = CreateService();
        var result = await service.LoginAsync(new LoginRequest { Usuario = "user1", Contrasena = "wrongpass" });

        Assert.False(result.Exitoso);
    }

    [Fact]
    public async Task Login_ValidCredentials_ReturnsSuccess()
    {
        var user = new Usuario
        {
            IdUsuario = 1,
            UsuarioNombre = "admin",
            Nombres = "Admin",
            Apellidos = "User",
            Correo = "admin@test.com",
            Activo = true,
            PasswordHash = "hashcorrect",
            UsuarioRoles = new List<UsuarioRol>()
        };

        _usuarioRepoMock.Setup(r => r.GetByUsernameAsync("admin")).ReturnsAsync(user);
        _passwordHasherMock.Setup(h => h.VerifyPassword("Admin123*", "hashcorrect")).Returns(true);
        _auditoriaRepoMock.Setup(r => r.AddSesionAsync(It.IsAny<AuditoriaSesion>())).Returns(Task.CompletedTask);
        _usuarioRepoMock.Setup(r => r.Update(It.IsAny<Usuario>()));
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(default)).ReturnsAsync(1);

        var service = CreateService();
        var result = await service.LoginAsync(new LoginRequest { Usuario = "admin", Contrasena = "Admin123*" });

        Assert.True(result.Exitoso);
        Assert.NotNull(result.Usuario);
        Assert.Equal("admin", result.Usuario.UsuarioNombre);
    }

    [Fact]
    public async Task DesactivarUsuario_UserNotFound_ThrowsKeyNotFoundException()
    {
        _usuarioRepoMock.Setup(r => r.GetByIdAsync(99)).ReturnsAsync((Usuario?)null);

        var service = CreateService();
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.DesactivarUsuarioAsync(99, 1, "127.0.0.1"));
    }

    [Fact]
    public async Task CambiarPassword_UserNotFound_ThrowsKeyNotFoundException()
    {
        _usuarioRepoMock.Setup(r => r.GetByIdAsync(99)).ReturnsAsync((Usuario?)null);

        var service = CreateService();
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.CambiarPasswordAsync(99, new CambiarPasswordRequest { NuevaContrasena = "newpass" }, 1, "127.0.0.1"));
    }
}
