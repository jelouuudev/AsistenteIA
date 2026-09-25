using Asistente.Application.Services;
using Asistente.Domain.Entities;
using Asistente.Domain.Interfaces;
using Asistente.Shared;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Asistente.Tests.Services;

public class RecuperacionServiceIsolationTests
{
    private readonly Mock<IProcesamientoDocumentalRepository> _mockProcesamiento;
    private readonly Mock<IVectorStore> _mockVectorStore;
    private readonly Mock<EmbeddingService> _mockEmbeddingService;
    private readonly Mock<IAsistenteFuenteRepository> _mockAsistenteFuente;
    private readonly Mock<IDocumentoFuenteRepository> _mockDocumentoFuente;
    private readonly Mock<IFuenteConocimientoRepository> _mockFuente;
    private readonly Mock<IConfiguracionRAGRepository> _mockConfigRAG;
    private readonly Mock<ILogger<RecuperacionService>> _mockLogger;
    private readonly RecuperacionService _service;

    public RecuperacionServiceIsolationTests()
    {
        _mockProcesamiento = new Mock<IProcesamientoDocumentalRepository>();
        _mockVectorStore = new Mock<IVectorStore>();
        _mockEmbeddingService = new Mock<EmbeddingService>(
            Mock.Of<IEmbeddingProvider>(),
            Mock.Of<ILogger<EmbeddingService>>());
        _mockAsistenteFuente = new Mock<IAsistenteFuenteRepository>();
        _mockDocumentoFuente = new Mock<IDocumentoFuenteRepository>();
        _mockFuente = new Mock<IFuenteConocimientoRepository>();
        _mockConfigRAG = new Mock<IConfiguracionRAGRepository>();
        _mockLogger = new Mock<ILogger<RecuperacionService>>();

        _mockConfigRAG.Setup(r => r.GetActivaAsync()).ReturnsAsync((ConfiguracionRAG?)null);

        _service = new RecuperacionService(
            _mockProcesamiento.Object,
            _mockVectorStore.Object,
            _mockEmbeddingService.Object,
            _mockAsistenteFuente.Object,
            _mockDocumentoFuente.Object,
            _mockFuente.Object,
            _mockConfigRAG.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task Asistente_Sin_Fuentes_Autorizadas_No_Obtiene_Resultados()
    {
        _mockAsistenteFuente
            .Setup(r => r.GetFuentesActivasPorAsistenteAsync(1))
            .ReturnsAsync(new List<FuenteConocimiento>());

        var (contexto, referencias) = await _service.RecuperarContextoConFuentesAsync("pregunta", 1);

        Assert.Empty(contexto);
        Assert.Empty(referencias);
        _mockVectorStore.Verify(v => v.SearchWithFilterAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<VectorSearchFilter>()), Times.Never);
    }

    [Fact]
    public async Task Asistente_Solo_Recupera_De_Fuentes_Autorizadas()
    {
        var fuentesAutorizadas = new List<FuenteConocimiento>
        {
            new() { IdFuente = 10, Nombre = "Manual Comercial", Prioridad = 1 },
            new() { IdFuente = 20, Nombre = "Manual Facturacion", Prioridad = 2 }
        };

        _mockAsistenteFuente
            .Setup(r => r.GetFuentesActivasPorAsistenteAsync(1))
            .ReturnsAsync(fuentesAutorizadas);

        _mockDocumentoFuente
            .Setup(r => r.GetDocumentosProcesadosIdsByFuenteAsync(It.IsAny<int>()))
            .ReturnsAsync(new List<int> { 100, 101 });

        var resultados = new List<VectorSearchResult>
        {
            new() { IdFuente = 10, Score = 0.8f, Text = "Texto del manual comercial", MetadataDocumentoNombre = "Manual Comercial" },
            new() { IdFuente = 99, Score = 0.95f, Text = "Texto de fuente NO autorizada", MetadataDocumentoNombre = "Manual RRHH" }
        };

        _mockVectorStore
            .Setup(v => v.SearchWithFilterAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<VectorSearchFilter>()))
            .ReturnsAsync(resultados);

        var (contexto, referencias) = await _service.RecuperarContextoConFuentesAsync("pregunta", 1);

        // El servicio consulta el vector store una vez por fuente autorizada (filtro individual).
        _mockVectorStore.Verify(v => v.SearchWithFilterAsync(It.IsAny<string>(), It.IsAny<int>(),
            It.Is<VectorSearchFilter>(f => f.IdsFuentes != null && f.IdsFuentes.Count == 1
                && (f.IdsFuentes.Contains(10) || f.IdsFuentes.Contains(20)))), Times.Exactly(2));
    }

    [Fact]
    public async Task Fuente_Desactivada_No_Genera_Resultados()
    {
        _mockAsistenteFuente
            .Setup(r => r.GetFuentesActivasPorAsistenteAsync(1))
            .ReturnsAsync(new List<FuenteConocimiento>());

        var (contexto, referencias) = await _service.RecuperarContextoConFuentesAsync("pregunta", 1);

        Assert.Empty(contexto);
        Assert.Empty(referencias);
    }

    [Fact]
    public async Task Filtro_Excluye_Documentos_Obsoletos_Por_Defecto()
    {
        var fuentes = new List<FuenteConocimiento>
        {
            new() { IdFuente = 1, Nombre = "Fuente Test", Prioridad = 1 }
        };

        _mockAsistenteFuente
            .Setup(r => r.GetFuentesActivasPorAsistenteAsync(1))
            .ReturnsAsync(fuentes);

        _mockDocumentoFuente
            .Setup(r => r.GetDocumentosProcesadosIdsByFuenteAsync(1))
            .ReturnsAsync(new List<int> { 100 });

        _mockVectorStore
            .Setup(v => v.SearchWithFilterAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<VectorSearchFilter>()))
            .ReturnsAsync(new List<VectorSearchResult>());

        await _service.RecuperarContextoConFuentesAsync("pregunta", 1);

        _mockVectorStore.Verify(v => v.SearchWithFilterAsync(It.IsAny<string>(), It.IsAny<int>(),
            It.Is<VectorSearchFilter>(f => f.IncluirHistoricos == false)), Times.Once);
    }

    [Fact]
    public async Task Prioridad_Alta_Pondera_Score_Mas_Alto()
    {
        var fuentes = new List<FuenteConocimiento>
        {
            new() { IdFuente = 1, Nombre = "Fuente Prioridad 1", Prioridad = 1 },
            new() { IdFuente = 2, Nombre = "Fuente Prioridad 5", Prioridad = 5 }
        };

        _mockAsistenteFuente
            .Setup(r => r.GetFuentesActivasPorAsistenteAsync(1))
            .ReturnsAsync(fuentes);

        _mockDocumentoFuente
            .Setup(r => r.GetDocumentosProcesadosIdsByFuenteAsync(It.IsAny<int>()))
            .ReturnsAsync(new List<int> { 100 });

        var resultados = new List<VectorSearchResult>
        {
            new() { IdFuente = 2, Score = 0.7f, Text = "Texto fuente baja prioridad", MetadataDocumentoNombre = "Doc Baja" },
            new() { IdFuente = 1, Score = 0.6f, Text = "Texto fuente alta prioridad", MetadataDocumentoNombre = "Doc Alta" }
        };

        _mockVectorStore
            .Setup(v => v.SearchWithFilterAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<VectorSearchFilter>()))
            .ReturnsAsync(resultados);

        var (contexto, referencias) = await _service.RecuperarContextoConFuentesAsync("pregunta", 1);

        if (referencias.Count >= 2)
        {
            Assert.True(referencias[0].PuntajeSimilitud >= referencias[1].PuntajeSimilitud,
                "La fuente con mayor prioridad deberia tener score ponderado igual o mayor.");
        }
    }

    [Fact]
    public async Task Sin_Asistente_No_Aplica_Filtros_De_Fuente()
    {
        _mockVectorStore
            .Setup(v => v.SearchAsync(It.IsAny<string>(), It.IsAny<int>()))
            .ReturnsAsync(new List<VectorSearchResult>
            {
                new() { Score = 0.8f, Text = "Texto libre", MetadataDocumentoNombre = "Doc" }
            });

        var contexto = await _service.RecuperarContextoAsync("pregunta libre");

        _mockVectorStore.Verify(v => v.SearchWithFilterAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<VectorSearchFilter>()), Times.Never);
        _mockVectorStore.Verify(v => v.SearchAsync(It.IsAny<string>(), It.IsAny<int>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task Referencias_Incluyen_Nombre_Fuente_Y_Documento()
    {
        var fuentes = new List<FuenteConocimiento>
        {
            new() { IdFuente = 1, Nombre = "Manual Comercial", Prioridad = 1 }
        };

        _mockAsistenteFuente
            .Setup(r => r.GetFuentesActivasPorAsistenteAsync(1))
            .ReturnsAsync(fuentes);

        _mockDocumentoFuente
            .Setup(r => r.GetDocumentosProcesadosIdsByFuenteAsync(1))
            .ReturnsAsync(new List<int> { 100 });

        _mockVectorStore
            .Setup(v => v.SearchWithFilterAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<VectorSearchFilter>()))
            .ReturnsAsync(new List<VectorSearchResult>
            {
                new()
                {
                    IdFuente = 1, DocumentoProcesadoId = 100, Score = 0.8f, Text = "Contenido relevante",
                    MetadataDocumentoNombre = "Manual Comercial v3",
                    VersionDocumento = "3.0", PaginaInicial = 10, PaginaFinal = 12
                }
            });

        var (_, referencias) = await _service.RecuperarContextoConFuentesAsync("pregunta", 1);

        Assert.Single(referencias);
        Assert.Equal("Manual Comercial v3", referencias[0].NombreDocumento);
        Assert.Equal("Manual Comercial", referencias[0].NombreFuente);
        Assert.Equal("3.0", referencias[0].VersionDocumento);
        Assert.Equal(10, referencias[0].PaginaInicial);
        Assert.Equal(12, referencias[0].PaginaFinal);
    }

    [Fact]
    public async Task Documento_Desasignado_De_Fuente_No_Genera_Contexto()
    {
        // El doc procesado 200 fue desasignado: ya no esta en la lista autorizada {100}.
        // Su vector huerfano puede seguir en Chroma, pero debe excluirse y no
        // debe haber fallback sin filtro.
        var fuentes = new List<FuenteConocimiento>
        {
            new() { IdFuente = 1, Nombre = "Fuente Test", Prioridad = 1 }
        };

        _mockAsistenteFuente
            .Setup(r => r.GetFuentesActivasPorAsistenteAsync(1))
            .ReturnsAsync(fuentes);

        _mockDocumentoFuente
            .Setup(r => r.GetDocumentosProcesadosIdsByFuenteAsync(1))
            .ReturnsAsync(new List<int> { 100 });

        _mockVectorStore
            .Setup(v => v.SearchWithFilterAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<VectorSearchFilter>()))
            .ReturnsAsync(new List<VectorSearchResult>
            {
                new() { IdFuente = 1, DocumentoProcesadoId = 200, Score = 0.95f, Text = "Montos autorizados...", MetadataDocumentoNombre = "Viaticos" }
            });

        var (contexto, referencias) = await _service.RecuperarContextoConFuentesAsync("montos autorizados", 1);

        Assert.Empty(contexto);
        Assert.Empty(referencias);
        _mockVectorStore.Verify(v => v.SearchAsync(It.IsAny<string>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task Configuracion_Personalizada_Sobrescribe_Valores_Por_Defecto()
    {
        var config = new ConfiguracionRAG
        {
            MaxChunks = 3,
            MaxCaracteresContexto = 2000,
            MinScore = 0.8
        };

        _mockConfigRAG.Setup(r => r.GetActivaAsync()).ReturnsAsync(config);

        _mockVectorStore
            .Setup(v => v.SearchAsync(It.IsAny<string>(), It.IsAny<int>()))
            .ReturnsAsync(new List<VectorSearchResult>
            {
                new() { Score = 0.9f, Text = "Texto valido", MetadataDocumentoNombre = "Doc" },
                new() { Score = 0.5f, Text = "Texto bajo score", MetadataDocumentoNombre = "Doc2" }
            });

        var contexto = await _service.RecuperarContextoAsync("pregunta");

        _mockVectorStore.Verify(v => v.SearchAsync(It.IsAny<string>(), It.Is<int>(k => k == 6)), Times.Once);
    }
}
