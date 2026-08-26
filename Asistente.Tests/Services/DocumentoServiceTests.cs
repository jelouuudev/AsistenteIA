using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Asistente.Application.Interfaces;
using Asistente.Application.Services;
using Asistente.Domain.Entities;
using Asistente.Domain.Enums;
using Asistente.Domain.Interfaces;
using Asistente.Shared;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Asistente.Tests.Services;

public class DocumentoServiceTests
{
    private readonly Mock<IDocumentoRepository> _documentoRepoMock = new();
    private readonly Mock<IDocumentoVersionRepository> _versionRepoMock = new();
    private readonly Mock<ICategoriaDocumentoRepository> _categoriaRepoMock = new();
    private readonly Mock<IAuditoriaDocumentalRepository> _auditoriaDocRepoMock = new();
    private readonly Mock<IProcesamientoDocumentalRepository> _procesamientoRepoMock = new();
    private readonly Mock<IFileStorageService> _fileStorageMock = new();
    private readonly Mock<IAuditoriaService> _auditoriaServiceMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<IDocumentoFuenteRepository> _documentoFuenteRepoMock = new();
    private readonly Mock<ILogger<DocumentoService>> _loggerMock = new();
    private readonly DocumentoService _service;

    public DocumentoServiceTests()
    {
        _service = new DocumentoService(
            _documentoRepoMock.Object,
            _versionRepoMock.Object,
            _categoriaRepoMock.Object,
            _auditoriaDocRepoMock.Object,
            _procesamientoRepoMock.Object,
            _fileStorageMock.Object,
            _auditoriaServiceMock.Object,
            _unitOfWorkMock.Object,
            _documentoFuenteRepoMock.Object,
            _loggerMock.Object);
    }

    private static Documento CreateDocumento(int id = 1, string codigo = "DOC-001", string nombre = "Manual",
        EstadoDocumento estado = EstadoDocumento.Activo, int idCategoria = 1)
    {
        return new Documento
        {
            IdDocumento = id,
            Codigo = codigo,
            Nombre = nombre,
            Descripcion = "Descripción test",
            IdCategoria = idCategoria,
            VersionActual = 0,
            Estado = estado,
            PendienteProcesamiento = true,
            FechaRegistro = DateTime.UtcNow,
            UsuarioRegistro = 1,
            Categoria = new CategoriaDocumento { IdCategoria = idCategoria, Nombre = "Manual Usuario" }
        };
    }

    private static CrearDocumentoRequest CreateRequest(string codigo = "DOC-001", string nombre = "Manual", int idCategoria = 1)
    {
        return new CrearDocumentoRequest
        {
            Codigo = codigo,
            Nombre = nombre,
            Descripcion = "Descripción",
            IdCategoria = idCategoria
        };
    }

    // --- ObtenerPorIdAsync ---

    [Fact]
    public async Task ObtenerPorIdAsync_Should_ReturnDocumentoDto_When_Exists()
    {
        var doc = CreateDocumento();
        _documentoRepoMock.Setup(x => x.GetByIdAsync(1)).ReturnsAsync(doc);

        var result = await _service.ObtenerPorIdAsync(1);

        Assert.NotNull(result);
        Assert.Equal("DOC-001", result!.Codigo);
        Assert.Equal("Manual", result.Nombre);
    }

    [Fact]
    public async Task ObtenerPorIdAsync_Should_ReturnNull_When_NotExists()
    {
        _documentoRepoMock.Setup(x => x.GetByIdAsync(999)).ReturnsAsync((Documento?)null);

        var result = await _service.ObtenerPorIdAsync(999);

        Assert.Null(result);
    }

    // --- ObtenerTodosAsync ---

    [Fact]
    public async Task ObtenerTodosAsync_Should_Return_AllDocumentos()
    {
        var docs = new List<Documento>
        {
            CreateDocumento(1, "DOC-001", "Manual 1"),
            CreateDocumento(2, "DOC-002", "Manual 2")
        };
        _documentoRepoMock.Setup(x => x.GetAllAsync()).ReturnsAsync(docs);

        var result = (await _service.ObtenerTodosAsync()).ToList();

        Assert.Equal(2, result.Count);
    }

    // --- ObtenerFiltradosAsync ---

    [Fact]
    public async Task ObtenerFiltradosAsync_Should_ReturnFiltered_Documentos()
    {
        var docs = new List<Documento> { CreateDocumento() };
        _documentoRepoMock.Setup(x => x.GetFilteredAsync(
            It.IsAny<string?>(), It.IsAny<int?>(),
            It.IsAny<EstadoDocumento?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
            .ReturnsAsync(docs);

        var filtro = new FiltroDocumentoRequest { Nombre = "Manual" };
        var result = (await _service.ObtenerFiltradosAsync(filtro)).ToList();

        Assert.Single(result);
    }

    // --- CrearAsync ---

    [Fact]
    public async Task CrearAsync_Should_CreateDocumento_When_Valid()
    {
        var request = CreateRequest();
        _documentoRepoMock.Setup(x => x.GetByCodigoAsync("DOC-001")).ReturnsAsync((Documento?)null);
        _categoriaRepoMock.Setup(x => x.GetByIdAsync(1)).ReturnsAsync(
            new CategoriaDocumento { IdCategoria = 1, Nombre = "Manual Usuario" });
        _documentoRepoMock.Setup(x => x.AddAsync(It.IsAny<Documento>()))
            .Callback<Documento>(d => d.IdDocumento = 1)
            .Returns(Task.CompletedTask);
        _documentoRepoMock.Setup(x => x.GetByIdAsync(1)).ReturnsAsync(CreateDocumento());

        var result = await _service.CrearAsync(request, 1, "127.0.0.1");

        Assert.NotNull(result);
        Assert.Equal("DOC-001", result.Codigo);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(), Times.AtLeastOnce);
    }

    [Fact]
    public async Task CrearAsync_Should_Throw_When_CodigoDuplicado()
    {
        var request = CreateRequest();
        _categoriaRepoMock.Setup(x => x.GetByIdAsync(1)).ReturnsAsync(
            new CategoriaDocumento { IdCategoria = 1, Nombre = "Manual Usuario" });
        _documentoRepoMock.Setup(x => x.GetByCodigoAsync("DOC-001")).ReturnsAsync(CreateDocumento());

        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.CrearAsync(request, 1, "127.0.0.1"));
    }

    [Fact]
    public async Task CrearAsync_Should_Throw_When_CategoriaNotFound()
    {
        var request = CreateRequest();
        _documentoRepoMock.Setup(x => x.GetByCodigoAsync("DOC-001")).ReturnsAsync((Documento?)null);
        _categoriaRepoMock.Setup(x => x.GetByIdAsync(1)).ReturnsAsync((CategoriaDocumento?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => _service.CrearAsync(request, 1, "127.0.0.1"));
    }

    // --- ActualizarAsync ---

    [Fact]
    public async Task ActualizarAsync_Should_UpdateDocumento_When_Valid()
    {
        var doc = CreateDocumento();
        _documentoRepoMock.Setup(x => x.GetByIdAsync(1)).ReturnsAsync(doc);
        _documentoRepoMock.Setup(x => x.GetByIdAsync(1)).ReturnsAsync(doc);
        var request = new ActualizarDocumentoRequest { Nombre = "Nuevo Nombre", Descripcion = "Desc", IdCategoria = 1 };

        await _service.ActualizarAsync(1, request, 1, "127.0.0.1");

        _documentoRepoMock.Verify(x => x.Update(It.IsAny<Documento>()), Times.Once);
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(), Times.AtLeastOnce);
    }

    [Fact]
    public async Task ActualizarAsync_Should_Throw_When_NotFound()
    {
        _documentoRepoMock.Setup(x => x.GetByIdAsync(999)).ReturnsAsync((Documento?)null);
        var request = new ActualizarDocumentoRequest { Nombre = "Nuevo", Descripcion = "Desc", IdCategoria = 1 };

        await Assert.ThrowsAsync<KeyNotFoundException>(() => _service.ActualizarAsync(999, request, 1, "127.0.0.1"));
    }

    [Fact]
    public async Task ActualizarAsync_Should_Throw_When_EstadoEliminado()
    {
        var doc = CreateDocumento(estado: EstadoDocumento.Eliminado);
        _documentoRepoMock.Setup(x => x.GetByIdAsync(1)).ReturnsAsync(doc);
        var request = new ActualizarDocumentoRequest { Nombre = "Nuevo", Descripcion = "Desc", IdCategoria = 1 };

        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.ActualizarAsync(1, request, 1, "127.0.0.1"));
    }

    // --- ActivarAsync ---

    [Fact]
    public async Task ActivarAsync_Should_SetEstadoActivo_When_Valid()
    {
        var doc = CreateDocumento(estado: EstadoDocumento.Activo);
        _documentoRepoMock.Setup(x => x.GetByIdAsync(1)).ReturnsAsync(doc);

        await _service.ActivarAsync(1, 1, "127.0.0.1");

        Assert.Equal(EstadoDocumento.Activo, doc.Estado);
        _documentoRepoMock.Verify(x => x.Update(doc), Times.Once);
    }

    [Fact]
    public async Task ActivarAsync_Should_Throw_When_NotFound()
    {
        _documentoRepoMock.Setup(x => x.GetByIdAsync(999)).ReturnsAsync((Documento?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => _service.ActivarAsync(999, 1, "127.0.0.1"));
    }

    [Fact]
    public async Task ActivarAsync_Should_Throw_When_EstadoEliminado()
    {
        var doc = CreateDocumento(estado: EstadoDocumento.Eliminado);
        _documentoRepoMock.Setup(x => x.GetByIdAsync(1)).ReturnsAsync(doc);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.ActivarAsync(1, 1, "127.0.0.1"));
    }

    // --- ArchivarAsync ---

    [Fact]
    public async Task ArchivarAsync_Should_SetEstadoArchivado_When_Valid()
    {
        var doc = CreateDocumento(estado: EstadoDocumento.Activo);
        _documentoRepoMock.Setup(x => x.GetByIdAsync(1)).ReturnsAsync(doc);

        await _service.ArchivarAsync(1, 1, "127.0.0.1");

        Assert.Equal(EstadoDocumento.Archivado, doc.Estado);
    }

    [Fact]
    public async Task ArchivarAsync_Should_Throw_When_NotFound()
    {
        _documentoRepoMock.Setup(x => x.GetByIdAsync(999)).ReturnsAsync((Documento?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => _service.ArchivarAsync(999, 1, "127.0.0.1"));
    }

    [Fact]
    public async Task ArchivarAsync_Should_Throw_When_EstadoEliminado()
    {
        var doc = CreateDocumento(estado: EstadoDocumento.Eliminado);
        _documentoRepoMock.Setup(x => x.GetByIdAsync(1)).ReturnsAsync(doc);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.ArchivarAsync(1, 1, "127.0.0.1"));
    }

    // --- EliminarAsync ---

    [Fact]
    public async Task EliminarAsync_Should_SetEstadoEliminado_When_Valid()
    {
        var doc = CreateDocumento(estado: EstadoDocumento.Activo);
        _documentoRepoMock.Setup(x => x.GetByIdAsync(1)).ReturnsAsync(doc);

        await _service.EliminarAsync(1, 1, "127.0.0.1");

        Assert.Equal(EstadoDocumento.Eliminado, doc.Estado);
    }

    [Fact]
    public async Task EliminarAsync_Should_Throw_When_NotFound()
    {
        _documentoRepoMock.Setup(x => x.GetByIdAsync(999)).ReturnsAsync((Documento?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => _service.EliminarAsync(999, 1, "127.0.0.1"));
    }

    // --- ObtenerVersionesAsync ---

    [Fact]
    public async Task ObtenerVersionesAsync_Should_Return_Versiones()
    {
        var versiones = new List<DocumentoVersion>
        {
            new DocumentoVersion { IdVersion = 1, IdDocumento = 1, NumeroVersion = 1, NombreArchivo = "v1.pdf", Activo = true }
        };
        _versionRepoMock.Setup(x => x.GetByDocumentoIdAsync(1)).ReturnsAsync(versiones);

        var result = (await _service.ObtenerVersionesAsync(1)).ToList();

        Assert.Single(result);
        Assert.Equal(1, result[0].NumeroVersion);
    }

    // --- CargarVersionAsync ---

    [Fact]
    public async Task CargarVersionAsync_Should_CreateNewVersion_When_Valid()
    {
        var doc = CreateDocumento();
        doc.VersionActual = 1;
        _documentoRepoMock.Setup(x => x.GetByIdAsync(1)).ReturnsAsync(doc);
        _fileStorageMock.Setup(x => x.ValidateFile(It.IsAny<string>(), It.IsAny<Stream>()));
        _fileStorageMock.Setup(x => x.SaveFileAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("/docs/doc_1/v2_test.pdf");

        using var stream = new MemoryStream(new byte[] { 1, 2, 3 });
        var result = await _service.CargarVersionAsync(1, "test.pdf", stream, 1, "127.0.0.1");

        Assert.NotNull(result);
        Assert.Equal(2, result.NumeroVersion);
        Assert.Equal("test.pdf", result.NombreArchivo);
        _versionRepoMock.Verify(x => x.AddAsync(It.IsAny<DocumentoVersion>()), Times.Once);
    }

    [Fact]
    public async Task CargarVersionAsync_Should_Throw_When_DocumentNotFound()
    {
        _documentoRepoMock.Setup(x => x.GetByIdAsync(999)).ReturnsAsync((Documento?)null);

        using var stream = new MemoryStream(new byte[] { 1, 2, 3 });
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _service.CargarVersionAsync(999, "test.pdf", stream, 1, "127.0.0.1"));
    }

    [Fact]
    public async Task CargarVersionAsync_Should_Throw_When_DocumentIsDeleted()
    {
        var doc = CreateDocumento(estado: EstadoDocumento.Eliminado);
        _documentoRepoMock.Setup(x => x.GetByIdAsync(1)).ReturnsAsync(doc);

        using var stream = new MemoryStream(new byte[] { 1, 2, 3 });
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.CargarVersionAsync(1, "test.pdf", stream, 1, "127.0.0.1"));
    }

    // --- DescargarVersionAsync ---

    [Fact]
    public async Task DescargarVersionAsync_Should_ReturnFileStream_When_VersionExists()
    {
        var version = new DocumentoVersion
        {
            IdVersion = 1, IdDocumento = 1, NumeroVersion = 1,
            NombreArchivo = "test.pdf", RutaArchivo = "/docs/test.pdf"
        };
        _versionRepoMock.Setup(x => x.GetByIdAsync(1)).ReturnsAsync(version);

        var stream = new MemoryStream(new byte[] { 1, 2, 3 });
        _fileStorageMock.Setup(x => x.GetFileAsync("/docs/test.pdf")).ReturnsAsync(stream);

        var result = await _service.DescargarVersionAsync(1, 1, 1, "127.0.0.1");

        Assert.Equal("test.pdf", result.fileName);
        Assert.Equal("application/pdf", result.contentType);
        Assert.NotNull(result.fileStream);
    }

    [Fact]
    public async Task DescargarVersionAsync_Should_Throw_When_VersionNotFound()
    {
        _versionRepoMock.Setup(x => x.GetByIdAsync(999)).ReturnsAsync((DocumentoVersion?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _service.DescargarVersionAsync(1, 999, 1, "127.0.0.1"));
    }

    [Fact]
    public async Task DescargarVersionAsync_Should_Throw_When_VersionBelongsToOtherDocument()
    {
        var version = new DocumentoVersion
        {
            IdVersion = 1, IdDocumento = 2, NumeroVersion = 1,
            NombreArchivo = "test.pdf", RutaArchivo = "/docs/test.pdf"
        };
        _versionRepoMock.Setup(x => x.GetByIdAsync(1)).ReturnsAsync(version);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _service.DescargarVersionAsync(1, 1, 1, "127.0.0.1"));
    }

    // --- ObtenerAuditoriaAsync ---

    [Fact]
    public async Task ObtenerAuditoriaAsync_Should_Return_Auditorias()
    {
        var auditorias = new List<AuditoriaDocumental>
        {
            new AuditoriaDocumental
            {
                IdAuditoria = 1, IdDocumento = 1, Accion = "Creación",
                Descripcion = "Test", UsuarioId = 1, FechaAccion = DateTime.UtcNow,
                Documento = CreateDocumento()
            }
        };
        _auditoriaDocRepoMock.Setup(x => x.GetByDocumentoIdAsync(1)).ReturnsAsync(auditorias);

        var result = (await _service.ObtenerAuditoriaAsync(1)).ToList();

        Assert.Single(result);
        Assert.Equal("Creación", result[0].Accion);
    }

    // --- ObtenerTodasAuditoriasAsync ---

    [Fact]
    public async Task ObtenerTodasAuditoriasAsync_Should_Return_AllAuditorias()
    {
        var auditorias = new List<AuditoriaDocumental>
        {
            new AuditoriaDocumental
            {
                IdAuditoria = 1, IdDocumento = 1, Accion = "Creación",
                Descripcion = "Test", UsuarioId = 1, FechaAccion = DateTime.UtcNow,
                Documento = CreateDocumento()
            }
        };
        _auditoriaDocRepoMock.Setup(x => x.GetAllAsync()).ReturnsAsync(auditorias);

        var result = (await _service.ObtenerTodasAuditoriasAsync()).ToList();

        Assert.Single(result);
    }
}
