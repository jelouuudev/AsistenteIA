using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Asistente.Application.Interfaces;
using Asistente.Application.Services;
using Asistente.Domain.Entities;
using Asistente.Domain.Interfaces;
using Asistente.Shared;
using Moq;
using Xunit;

namespace Asistente.Tests.Services;

public class CategoriaDocumentoServiceTests
{
    private readonly Mock<ICategoriaDocumentoRepository> _categoriaRepoMock = new();
    private readonly Mock<IAuditoriaService> _auditoriaServiceMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly CategoriaDocumentoService _service;

    public CategoriaDocumentoServiceTests()
    {
        _service = new CategoriaDocumentoService(
            _categoriaRepoMock.Object,
            _auditoriaServiceMock.Object,
            _unitOfWorkMock.Object);
    }

    private static CategoriaDocumento CreateCategoria(int id = 1, string nombre = "Manual Usuario", bool activo = true)
    {
        return new CategoriaDocumento
        {
            IdCategoria = id,
            Nombre = nombre,
            Descripcion = "Descripción test",
            Activo = activo
        };
    }

    // --- ObtenerPorIdAsync ---

    [Fact]
    public async Task ObtenerPorIdAsync_Should_ReturnDto_When_Exists()
    {
        var cat = CreateCategoria();
        _categoriaRepoMock.Setup(x => x.GetByIdAsync(1)).ReturnsAsync(cat);

        var result = await _service.ObtenerPorIdAsync(1);

        Assert.NotNull(result);
        Assert.Equal("Manual Usuario", result!.Nombre);
    }

    [Fact]
    public async Task ObtenerPorIdAsync_Should_ReturnNull_When_NotExists()
    {
        _categoriaRepoMock.Setup(x => x.GetByIdAsync(999)).ReturnsAsync((CategoriaDocumento?)null);

        var result = await _service.ObtenerPorIdAsync(999);

        Assert.Null(result);
    }

    // --- ObtenerTodasAsync ---

    [Fact]
    public async Task ObtenerTodasAsync_Should_Return_AllCategorias()
    {
        var cats = new List<CategoriaDocumento>
        {
            CreateCategoria(1, "Manual Usuario"),
            CreateCategoria(2, "Políticas")
        };
        _categoriaRepoMock.Setup(x => x.GetAllAsync()).ReturnsAsync(cats);

        var result = (await _service.ObtenerTodasAsync()).ToList();

        Assert.Equal(2, result.Count);
    }

    // --- ObtenerActivasAsync ---

    [Fact]
    public async Task ObtenerActivasAsync_Should_Return_OnlyActiveCategorias()
    {
        var cats = new List<CategoriaDocumento>
        {
            CreateCategoria(1, "Manual Usuario", true),
            CreateCategoria(2, "Políticas", false)
        };
        _categoriaRepoMock.Setup(x => x.GetAllActivasAsync()).ReturnsAsync(cats);

        var result = (await _service.ObtenerActivasAsync()).ToList();

        Assert.Equal(2, result.Count);
    }

    // --- CrearAsync ---

    [Fact]
    public async Task CrearAsync_Should_CreateCategoria_When_Valid()
    {
        _categoriaRepoMock.Setup(x => x.GetByNombreAsync("Nueva Categoría")).ReturnsAsync((CategoriaDocumento?)null);
        _categoriaRepoMock.Setup(x => x.AddAsync(It.IsAny<CategoriaDocumento>()))
            .Callback<CategoriaDocumento>(c => c.IdCategoria = 1)
            .Returns(Task.CompletedTask);

        var request = new CrearCategoriaDocumentoRequest { Nombre = "Nueva Categoría", Descripcion = "Test" };
        var result = await _service.CrearAsync(request, 1, "127.0.0.1");

        Assert.NotNull(result);
        Assert.Equal("Nueva Categoría", result.Nombre);
        Assert.True(result.Activo);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(), Times.Once);
        _auditoriaServiceMock.Verify(x => x.RegistrarActividadAsync(
            1, "GestorDocumental", "CrearCategoria", It.IsAny<string>(), "127.0.0.1"), Times.Once);
    }

    [Fact]
    public async Task CrearAsync_Should_Throw_When_NombreDuplicado()
    {
        _categoriaRepoMock.Setup(x => x.GetByNombreAsync("Existente")).ReturnsAsync(CreateCategoria());

        var request = new CrearCategoriaDocumentoRequest { Nombre = "Existente", Descripcion = "Test" };
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.CrearAsync(request, 1, "127.0.0.1"));
    }

    // --- ActualizarAsync ---

    [Fact]
    public async Task ActualizarAsync_Should_UpdateCategoria_When_Valid()
    {
        var cat = CreateCategoria();
        _categoriaRepoMock.Setup(x => x.GetByIdAsync(1)).ReturnsAsync(cat);

        var request = new ActualizarCategoriaDocumentoRequest
        {
            Nombre = "Nombre Actualizado",
            Descripcion = "Desc actualizada",
            Activo = false
        };
        var result = await _service.ActualizarAsync(1, request, 1, "127.0.0.1");

        Assert.Equal("Nombre Actualizado", result.Nombre);
        Assert.False(result.Activo);
        _categoriaRepoMock.Verify(x => x.Update(cat), Times.Once);
    }

    [Fact]
    public async Task ActualizarAsync_Should_Throw_When_NotFound()
    {
        _categoriaRepoMock.Setup(x => x.GetByIdAsync(999)).ReturnsAsync((CategoriaDocumento?)null);

        var request = new ActualizarCategoriaDocumentoRequest
        {
            Nombre = "Nuevo",
            Descripcion = "Desc",
            Activo = true
        };
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _service.ActualizarAsync(999, request, 1, "127.0.0.1"));
    }
}
