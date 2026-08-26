using Asistente.Application.Services;
using Asistente.Domain.Entities;
using Asistente.Domain.Interfaces;
using Asistente.Shared;
using Moq;
using Xunit;

namespace Asistente.Tests.Services;

public class PromptSistemaServiceTests
{
    private readonly Mock<IPromptSistemaRepository> _mockPromptRepo;
    private readonly Mock<IHistorialPromptRepository> _mockHistorialRepo;
    private readonly Mock<IUnitOfWork> _mockUow;
    private readonly PromptSistemaService _service;

    public PromptSistemaServiceTests()
    {
        _mockPromptRepo = new Mock<IPromptSistemaRepository>();
        _mockHistorialRepo = new Mock<IHistorialPromptRepository>();
        _mockUow = new Mock<IUnitOfWork>();
        _service = new PromptSistemaService(_mockPromptRepo.Object, _mockHistorialRepo.Object, _mockUow.Object);
    }

    [Fact]
    public async Task ObtenerPorIdAsync_Should_Return_Prompt_When_Exists()
    {
        var prompt = new PromptSistema
        {
            IdPrompt = 1,
            IdAsistente = 1,
            Nombre = "Prompt Test",
            Contenido = "Contenido",
            Version = 1,
            Activo = true
        };
        _mockPromptRepo.Setup(x => x.GetByIdAsync(1)).ReturnsAsync(prompt);

        var result = await _service.ObtenerPorIdAsync(1);

        Assert.NotNull(result);
        Assert.Equal("Prompt Test", result!.Nombre);
    }

    [Fact]
    public async Task ObtenerActivoPorAsistenteIdAsync_Should_Return_Active_Prompt()
    {
        var prompt = new PromptSistema
        {
            IdPrompt = 1,
            IdAsistente = 1,
            Nombre = "Activo",
            Contenido = "Contenido",
            Activo = true,
            Version = 2
        };
        _mockPromptRepo.Setup(x => x.GetActiveByAsistenteIdAsync(1)).ReturnsAsync(prompt);

        var result = await _service.ObtenerActivoPorAsistenteIdAsync(1);

        Assert.NotNull(result);
        Assert.True(result!.Activo);
    }

    [Fact]
    public async Task CrearPromptAsync_Should_Create_With_Version_And_Historial()
    {
        _mockPromptRepo.Setup(x => x.GetNextVersionAsync(1)).ReturnsAsync(1);
        _mockPromptRepo.Setup(x => x.AddAsync(It.IsAny<PromptSistema>())).Returns(Task.CompletedTask);
        _mockHistorialRepo.Setup(x => x.AddAsync(It.IsAny<HistorialPrompt>())).Returns(Task.CompletedTask);
        _mockUow.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var request = new CrearPromptRequest
        {
            IdAsistente = 1,
            Nombre = "Prompt 1",
            Contenido = "Contenido del prompt",
            UsuarioCreacion = "admin"
        };

        var result = await _service.CrearPromptAsync(request);

        Assert.Equal("Prompt 1", result.Nombre);
        Assert.Equal(1, result.Version);
        Assert.True(result.Activo);

        _mockHistorialRepo.Verify(x => x.AddAsync(It.IsAny<HistorialPrompt>()), Times.Once);
    }

    [Fact]
    public async Task ActualizarPromptAsync_Should_Increment_Version_And_Add_Historial()
    {
        var prompt = new PromptSistema
        {
            IdPrompt = 1,
            IdAsistente = 1,
            Nombre = "Original",
            Contenido = "Original content",
            Version = 1,
            Activo = true
        };

        _mockPromptRepo.Setup(x => x.GetByIdAsync(1)).ReturnsAsync(prompt);
        _mockPromptRepo.Setup(x => x.GetNextVersionAsync(1)).ReturnsAsync(2);
        _mockHistorialRepo.Setup(x => x.AddAsync(It.IsAny<HistorialPrompt>())).Returns(Task.CompletedTask);
        _mockUow.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var request = new ActualizarPromptRequest
        {
            Nombre = "Actualizado",
            Contenido = "Updated content",
            Activo = true,
            MotivoCambio = "Se corrigieron errores"
        };

        var result = await _service.ActualizarPromptAsync(1, request, "admin");

        Assert.Equal("Actualizado", result.Nombre);
        Assert.Equal(2, result.Version);

        _mockHistorialRepo.Verify(x => x.AddAsync(It.IsAny<HistorialPrompt>()), Times.Exactly(2));
    }

    [Fact]
    public async Task ActivarPromptAsync_Should_Deactivate_Others_And_Activate_Selected()
    {
        var prompt = new PromptSistema
        {
            IdPrompt = 2,
            IdAsistente = 1,
            Activo = false
        };

        var otrosPrompts = new List<PromptSistema>
        {
            new() { IdPrompt = 1, IdAsistente = 1, Activo = true }
        };

        _mockPromptRepo.Setup(x => x.GetByIdAsync(2)).ReturnsAsync(prompt);
        _mockPromptRepo.Setup(x => x.GetByAsistenteIdAsync(1)).ReturnsAsync(otrosPrompts);
        _mockUow.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        await _service.ActivarPromptAsync(2, "admin");

        Assert.True(prompt.Activo);
        Assert.False(otrosPrompts[0].Activo);
    }

    [Fact]
    public async Task ObtenerHistorialAsync_Should_Return_Historial_Ordered()
    {
        var historial = new List<HistorialPrompt>
        {
            new() { IdHistorial = 1, IdPrompt = 1, Version = 1, Contenido = "v1", UsuarioModificacion = "admin" },
            new() { IdHistorial = 2, IdPrompt = 1, Version = 2, Contenido = "v2", UsuarioModificacion = "admin" }
        };

        _mockHistorialRepo.Setup(x => x.GetByPromptIdAsync(1)).ReturnsAsync(historial);

        var result = await _service.ObtenerHistorialAsync(1);

        Assert.Equal(2, result.Count());
    }
}
