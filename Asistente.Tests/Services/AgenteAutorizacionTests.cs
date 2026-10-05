using Asistente.Application.Services.Seguridad;
using Asistente.Domain.Entities;
using Asistente.Domain.Interfaces;
using Moq;
using Xunit;

namespace Asistente.Tests.Services;

/// <summary>
/// Pruebas de autorización de agentes (ETAPA 16 - Regla 1): un usuario accede
/// a un agente si está asignado directamente a él o si el agente está asignado
/// a alguno de sus roles. Sin bypass: las asignaciones mandan para todos,
/// incluido el Administrador.
/// </summary>
public class AgenteAutorizacionTests
{
    private readonly Mock<IUsuarioRepository> _mockUsuarioRepo;
    private readonly Mock<IUsuarioAsistenteRepository> _mockUsuarioAsistenteRepo;
    private readonly Mock<IUsuarioFuenteRepository> _mockUsuarioFuenteRepo;
    private readonly Mock<IPermisoRepository> _mockPermisoRepo;
    private readonly Mock<IAsistenteRepository> _mockAsistenteRepo;
    private readonly AutorizacionService _service;

    public AgenteAutorizacionTests()
    {
        _mockUsuarioRepo = new Mock<IUsuarioRepository>();
        _mockUsuarioAsistenteRepo = new Mock<IUsuarioAsistenteRepository>();
        _mockUsuarioFuenteRepo = new Mock<IUsuarioFuenteRepository>();
        _mockPermisoRepo = new Mock<IPermisoRepository>();
        _mockAsistenteRepo = new Mock<IAsistenteRepository>();
        _service = new AutorizacionService(
            _mockUsuarioRepo.Object,
            _mockUsuarioAsistenteRepo.Object,
            _mockUsuarioFuenteRepo.Object,
            _mockPermisoRepo.Object,
            _mockAsistenteRepo.Object);
    }

    private static Usuario UsuarioConRol(int idUsuario, int idRol)
    {
        return new Usuario
        {
            IdUsuario = idUsuario,
            UsuarioRoles = new List<UsuarioRol> { new() { IdUsuario = idUsuario, IdRol = idRol, Rol = new Rol { IdRol = idRol, Nombre = "Operador" } } }
        };
    }

    [Fact]
    public async Task VerificarAsistenteAsync_AdminSinAsignacion_Denegado()
    {
        _mockUsuarioRepo.Setup(x => x.GetByIdAsync(1)).ReturnsAsync(
            new Usuario { IdUsuario = 1, UsuarioRoles = new List<UsuarioRol> { new() { IdRol = 1, Rol = new Rol { IdRol = 1, Nombre = "Administrador" } } } });
        _mockUsuarioAsistenteRepo.Setup(x => x.EstaAutorizadoAsync(1, 10, It.IsAny<System.Threading.CancellationToken>())).ReturnsAsync(false);
        _mockAsistenteRepo.Setup(x => x.GetByIdAsync(10)).ReturnsAsync(
            new Domain.Entities.Asistente { IdAsistente = 10, AgentesRoles = new List<AgenteRol>() });

        var result = await _service.VerificarAsistenteAsync(1, 10);

        Assert.False(result.Permitido);
    }

    [Fact]
    public async Task VerificarAsistenteAsync_UsuarioAsignadoDirecto_Permitido()
    {
        _mockUsuarioRepo.Setup(x => x.GetByIdAsync(5)).ReturnsAsync(UsuarioConRol(5, 2));
        _mockUsuarioAsistenteRepo.Setup(x => x.EstaAutorizadoAsync(5, 3, It.IsAny<System.Threading.CancellationToken>())).ReturnsAsync(true);

        var result = await _service.VerificarAsistenteAsync(5, 3);

        Assert.True(result.Permitido);
    }

    [Fact]
    public async Task VerificarAsistenteAsync_AgenteAsignadoARolDelUsuario_Permitido()
    {
        _mockUsuarioRepo.Setup(x => x.GetByIdAsync(5)).ReturnsAsync(UsuarioConRol(5, 7));
        _mockUsuarioAsistenteRepo.Setup(x => x.EstaAutorizadoAsync(5, 3, It.IsAny<System.Threading.CancellationToken>())).ReturnsAsync(false);
        _mockAsistenteRepo.Setup(x => x.GetByIdAsync(3)).ReturnsAsync(
            new Domain.Entities.Asistente { IdAsistente = 3, AgentesRoles = new List<AgenteRol> { new() { IdRol = 7, Activo = true } } });

        var result = await _service.VerificarAsistenteAsync(5, 3);

        Assert.True(result.Permitido);
    }

    [Fact]
    public async Task VerificarAsistenteAsync_SinAsignacion_Denegado()
    {
        _mockUsuarioRepo.Setup(x => x.GetByIdAsync(5)).ReturnsAsync(UsuarioConRol(5, 2));
        _mockUsuarioAsistenteRepo.Setup(x => x.EstaAutorizadoAsync(5, 3, It.IsAny<System.Threading.CancellationToken>())).ReturnsAsync(false);
        _mockAsistenteRepo.Setup(x => x.GetByIdAsync(3)).ReturnsAsync(
            new Domain.Entities.Asistente { IdAsistente = 3, AgentesRoles = new List<AgenteRol> { new() { IdRol = 99, Activo = true } } });

        var result = await _service.VerificarAsistenteAsync(5, 3);

        Assert.False(result.Permitido);
    }
}
