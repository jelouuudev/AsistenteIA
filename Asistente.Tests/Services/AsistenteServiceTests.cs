using Asistente.Application.Services;
using Asistente.Domain.Interfaces;
using Asistente.Shared;
using Moq;
using Xunit;

namespace Asistente.Tests.Services;

public class AsistenteServiceTests
{
    private readonly Mock<IAsistenteRepository> _mockRepo;
    private readonly Mock<IUnitOfWork> _mockUow;
    private readonly AsistenteService _service;

    public AsistenteServiceTests()
    {
        _mockRepo = new Mock<IAsistenteRepository>();
        _mockUow = new Mock<IUnitOfWork>();
        _service = new AsistenteService(_mockRepo.Object, _mockUow.Object);
    }

    [Fact]
    public async Task ObtenerTodosAsync_Should_Return_All_Asistentes()
    {
        var asistentes = new List<Domain.Entities.Asistente>
        {
            new() { IdAsistente = 1, Nombre = "Comercial", ModeloIA = "qwen2.5:7b", Activo = true },
            new() { IdAsistente = 2, Nombre = "RRHH", ModeloIA = "qwen2.5:7b", Activo = true }
        };
        _mockRepo.Setup(x => x.GetAllAsync()).ReturnsAsync(asistentes);

        var result = await _service.ObtenerTodosAsync();

        Assert.Equal(2, result.Count());
    }

    [Fact]
    public async Task ObtenerPorIdAsync_Should_Return_Asistente_When_Exists()
    {
        var asistente = new Domain.Entities.Asistente
        {
            IdAsistente = 1,
            Nombre = "Comercial",
            ModeloIA = "qwen2.5:7b",
            Activo = true
        };
        _mockRepo.Setup(x => x.GetByIdAsync(1)).ReturnsAsync(asistente);

        var result = await _service.ObtenerPorIdAsync(1);

        Assert.NotNull(result);
        Assert.Equal("Comercial", result!.Nombre);
    }

    [Fact]
    public async Task ObtenerPorIdAsync_Should_Return_Null_When_Not_Exists()
    {
        _mockRepo.Setup(x => x.GetByIdAsync(99)).ReturnsAsync((Domain.Entities.Asistente?)null);

        var result = await _service.ObtenerPorIdAsync(99);

        Assert.Null(result);
    }

    [Fact]
    public async Task CrearAsistenteAsync_Should_Create_And_Return_Dto()
    {
        var request = new CrearAsistenteRequest
        {
            Nombre = "Test",
            ModeloIA = "qwen2.5:7b",
            Idioma = "es",
            NivelFormalidad = "profesional"
        };

        _mockRepo.Setup(x => x.AddAsync(It.IsAny<Domain.Entities.Asistente>())).Returns(Task.CompletedTask);
        _mockUow.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var result = await _service.CrearAsistenteAsync(request);

        Assert.NotNull(result);
        Assert.Equal("Test", result.Nombre);
        Assert.Equal("qwen2.5:7b", result.ModeloIA);
        Assert.True(result.Activo);
    }

    [Fact]
    public async Task DesactivarAsistenteAsync_Should_Set_Activo_False()
    {
        var asistente = new Domain.Entities.Asistente
        {
            IdAsistente = 1,
            Nombre = "Test",
            Activo = true
        };
        _mockRepo.Setup(x => x.GetByIdAsync(1)).ReturnsAsync(asistente);
        _mockUow.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        await _service.DesactivarAsistenteAsync(1);

        Assert.False(asistente.Activo);
    }

    [Fact]
    public async Task DesactivarAsistenteAsync_Should_Throw_When_Not_Exists()
    {
        _mockRepo.Setup(x => x.GetByIdAsync(99)).ReturnsAsync((Domain.Entities.Asistente?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => _service.DesactivarAsistenteAsync(99));
    }

    [Fact]
    public async Task ActivarAsistenteAsync_Should_Set_Activo_True()
    {
        var asistente = new Domain.Entities.Asistente
        {
            IdAsistente = 1,
            Nombre = "Test",
            Activo = false
        };
        _mockRepo.Setup(x => x.GetByIdAsync(1)).ReturnsAsync(asistente);
        _mockUow.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        await _service.ActivarAsistenteAsync(1);

        Assert.True(asistente.Activo);
    }

    [Fact]
    public async Task ActualizarAsistenteAsync_Should_Update_All_Properties()
    {
        var asistente = new Domain.Entities.Asistente
        {
            IdAsistente = 1,
            Nombre = "Original",
            ModeloIA = "qwen2.5:7b",
            Activo = true
        };
        _mockRepo.Setup(x => x.GetByIdAsync(1)).ReturnsAsync(asistente);
        _mockUow.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var request = new ActualizarAsistenteRequest
        {
            Nombre = "Actualizado",
            Descripcion = "Nueva desc",
            ModeloIA = "llama3",
            Activo = false,
            Idioma = "en"
        };

        var result = await _service.ActualizarAsistenteAsync(1, request);

        Assert.Equal("Actualizado", result.Nombre);
        Assert.Equal("llama3", result.ModeloIA);
        Assert.False(result.Activo);
        Assert.Equal("en", result.Idioma);
    }
}
