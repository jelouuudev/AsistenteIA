using Microsoft.Extensions.DependencyInjection;
using Asistente.Application.Interfaces;
using Asistente.Application.Services;
using Asistente.Application.Services.Workflows;
using Asistente.Domain.Entities;
using Asistente.Domain.Enums;
using Asistente.Domain.Interfaces;
using Asistente.Shared;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Asistente.Tests.Services;

public class ChatServiceTests
{
    private readonly Mock<IOllamaService> _mockOllamaService;
    private readonly Mock<IConversacionRepository> _mockConversationRepository;
    private readonly Mock<IMensajeRepository> _mockMessageRepository;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<ILogger<ChatService>> _mockLogger;
    private readonly Mock<AsistenteService> _mockAsistenteService;
    private readonly Mock<PromptSistemaService> _mockPromptService;
    private readonly Mock<ContextoService> _mockContextoService;
    private readonly Mock<IRecuperacionService> _mockRecuperacionService;
    private readonly Mock<IQueryEmpresarialService> _mockQueryEmpresarialService;
    private readonly Mock<IDecisionHerramientaService> _mockDecisionService;
    private readonly Mock<IToolOrchestrator> _mockOrchestrator;
    private readonly Mock<IWorkflowDecisionService> _mockWorkflowDecision;
    private readonly Mock<IWorkflowEngine> _mockWorkflowEngine;
    private readonly Mock<IAgentOrchestrator> _mockAgentOrchestrator;
    private readonly IChatService _chatService;

    public ChatServiceTests()
    {
        _mockOllamaService = new Mock<IOllamaService>();
        _mockConversationRepository = new Mock<IConversacionRepository>();
        _mockMessageRepository = new Mock<IMensajeRepository>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockLogger = new Mock<ILogger<ChatService>>();

        _mockAsistenteService = new Mock<AsistenteService>(
            Mock.Of<Domain.Interfaces.IAsistenteRepository>(),
            Mock.Of<Domain.Interfaces.IUnitOfWork>());

        _mockPromptService = new Mock<PromptSistemaService>(
            Mock.Of<Domain.Interfaces.IPromptSistemaRepository>(),
            Mock.Of<Domain.Interfaces.IHistorialPromptRepository>(),
            Mock.Of<Domain.Interfaces.IUnitOfWork>());

        _mockRecuperacionService = new Mock<IRecuperacionService>();
        _mockQueryEmpresarialService = new Mock<IQueryEmpresarialService>();
        _mockQueryEmpresarialService.Setup(s => s.ProcesarPreguntaAsync(
            It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResultadoProcesarPreguntaDto { Tipo = "documental", Exitoso = true, Fuente = "rag" });
        _mockDecisionService = new Mock<IDecisionHerramientaService>();
        _mockOrchestrator = new Mock<IToolOrchestrator>();
        _mockWorkflowDecision = new Mock<IWorkflowDecisionService>();
        _mockWorkflowEngine = new Mock<IWorkflowEngine>();
        var _mockAutorizacion = new Mock<IAutorizacionService>();
        _mockAutorizacion.Setup(a => a.VerificarAsistenteAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync(new ResultadoAutorizacion { Permitido = true });
        _mockAutorizacion.Setup(a => a.VerificarFuenteAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync(new ResultadoAutorizacion { Permitido = true });
        _mockAutorizacion.Setup(a => a.VerificarHerramientaAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync(new ResultadoAutorizacion { Permitido = true });
        var _mockProteccion = new Mock<IProteccionDatosService>();
        _mockProteccion.Setup(p => p.Enmascarar(It.IsAny<string>())).Returns((string s) => s);
        var _mockInjection = new Mock<IPromptInjectionService>();
        _mockInjection.Setup(p => p.EsMalicioso(It.IsAny<string>(), out It.Ref<string>.IsAny))
            .Returns((string s, out string r) => { r = string.Empty; return false; });
        var _mockRate = new Mock<IRateLimitService>();
        _mockRate.Setup(r => r.RegistrarYVerificar(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
            .Returns((string k, int m, int w) => (true, m, 0));
        var _mockAuditoriaIA = new Mock<IAuditoriaIARepository>();
        _mockAuditoriaIA.Setup(r => r.AddAsync(It.IsAny<AuditoriaIA>(), It.IsAny<System.Threading.CancellationToken>())).Returns(System.Threading.Tasks.Task.CompletedTask);
        var _mockMetricas = new Mock<IMetricasIARepository>();
        _mockMetricas.Setup(r => r.AddAsync(It.IsAny<MetricasIA>(), It.IsAny<System.Threading.CancellationToken>())).Returns(System.Threading.Tasks.Task.CompletedTask);
        var _mockUsuarioFuente = new Mock<IUsuarioFuenteRepository>();
        _mockUsuarioFuente.Setup(r => r.GetFuentesAutorizadasAsync(It.IsAny<int>(), It.IsAny<System.Threading.CancellationToken>())).ReturnsAsync(new List<int>());
        _mockAgentOrchestrator = new Mock<IAgentOrchestrator>();
        _mockWorkflowEngine = new Mock<IWorkflowEngine>();
        _mockWorkflowEngine.Setup(w => w.BuscarPorDisparadorAsync(It.IsAny<string>(), It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync((Workflow?)null);

        // Asistente por defecto activo (el flujo actual exige un agente disponible).
        var asistenteTest = new AsistenteDto
        {
            IdAsistente = 1,
            Codigo = "TEST-01",
            Nombre = "Test",
            Activo = true,
            Estado = EstadoAgente.Activo
        };
        _mockAsistenteService.Setup(s => s.ObtenerTodosAsync()).ReturnsAsync(new List<AsistenteDto> { asistenteTest });
        _mockAsistenteService.Setup(s => s.ObtenerPorIdAsync(It.IsAny<int>())).ReturnsAsync(asistenteTest);

        _mockPromptService.Setup(p => p.ObtenerActivoPorAsistenteIdAsync(It.IsAny<int>()))
            .ReturnsAsync((PromptSistemaDto?)null);

        _mockContextoService = new Mock<ContextoService>(
            Mock.Of<Asistente.Domain.Interfaces.IConfiguracionMemoriaRepository>(),
            Mock.Of<Asistente.Domain.Interfaces.IMensajeRepository>(),
            Mock.Of<Microsoft.Extensions.Logging.ILogger<Asistente.Application.Services.ContextoService>>());
        _mockContextoService.Setup(c => c.ConstruirContextoAsync(It.IsAny<Conversacion>(), It.IsAny<Mensaje>(), It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync(new ContextoConstruido { MensajesRecientes = new List<Mensaje>() });
        _mockContextoService.Setup(c => c.RequiereResumenAsync(It.IsAny<Conversacion>(), It.IsAny<int>()))
            .ReturnsAsync(false);

        _mockOrchestrator.Setup(o => o.ObtenerHerramientasParaAsistenteAsync(It.IsAny<int>(), It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync(new List<Herramienta>());
        _mockRecuperacionService.Setup(r => r.RecuperarContextoConFuentesAsync(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync((string.Empty, new List<ReferenciaDocumentalDto>()));
        _mockMessageRepository.Setup(x => x.GetByConversacionIdAsync(It.IsAny<int>()))
            .ReturnsAsync(new List<Mensaje>());

        _chatService = new ChatService(
            _mockConversationRepository.Object,
            _mockMessageRepository.Object,
            _mockOllamaService.Object,
            _mockUnitOfWork.Object,
            _mockLogger.Object,
            _mockAsistenteService.Object,
            _mockPromptService.Object,
            _mockContextoService.Object,
            Mock.Of<Asistente.Application.Interfaces.IMemoriaService>(),
            _mockRecuperacionService.Object,
            _mockQueryEmpresarialService.Object,
            _mockDecisionService.Object,
            _mockOrchestrator.Object,
            _mockWorkflowDecision.Object,
            _mockWorkflowEngine.Object,
            _mockAutorizacion.Object,
            _mockProteccion.Object,
            _mockInjection.Object,
            _mockRate.Object,
            _mockAuditoriaIA.Object,
            _mockMetricas.Object,
            _mockUsuarioFuente.Object,
            new Lazy<Asistente.Application.Interfaces.IAgentOrchestrator>(() => _mockAgentOrchestrator.Object),
            new Mock<Microsoft.Extensions.DependencyInjection.IServiceScopeFactory>().Object);
    }

    [Fact]
    public async Task ProcessMessageAsync_Should_Return_Success_When_Message_Is_Valid()
    {
        // Arrange
        var request = new MensajeRequest
        {
            Mensaje = "Hola, ¿cómo estás?",
            IdConversacion = 1
        };

        var conversacionExistente = new Conversacion
        {
            IdConversacion = 1,
            FechaInicio = DateTime.UtcNow,
            Estado = EstadoConversacion.Activa,
            Mensajes = new List<Mensaje>()
        };

        _mockConversationRepository
            .Setup(x => x.GetByIdAsync(1))
            .ReturnsAsync(conversacionExistente);

        _mockMessageRepository
            .Setup(x => x.CreateAsync(It.IsAny<Mensaje>()))
            .ReturnsAsync(new Mensaje());

        _mockOllamaService
            .Setup(x => x.SendMessageAsync(It.IsAny<IEnumerable<Mensaje>>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<double?>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("¡Hola! Estoy bien, ¿y tú?");

        _mockUnitOfWork
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _chatService.ProcesarMensajeAsync(request);

        // Assert - Verificar que se llamó a los servicios
        _mockOllamaService.Verify(x => x.SendMessageAsync(It.IsAny<IEnumerable<Mensaje>>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<double?>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockUnitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task ProcessMessageAsync_Should_Use_Existing_Conversation_When_ConversationId_Is_Provided()
    {
        // Arrange
        var request = new MensajeRequest
        {
            Mensaje = "¿Puedes ayudarme?",
            IdConversacion = 1
        };

        var conversacionExistente = new Conversacion
        {
            IdConversacion = 1,
            FechaInicio = DateTime.UtcNow,
            Estado = EstadoConversacion.Activa,
            Mensajes = new List<Mensaje>()
        };

        _mockConversationRepository
            .Setup(x => x.GetByIdAsync(1))
            .ReturnsAsync(conversacionExistente);

        _mockMessageRepository
            .Setup(x => x.CreateAsync(It.IsAny<Mensaje>()))
            .ReturnsAsync(new Mensaje());

        _mockOllamaService
            .Setup(x => x.SendMessageAsync(It.IsAny<IEnumerable<Mensaje>>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<double?>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Por supuesto, ¿en qué puedo ayudarte?");

        _mockUnitOfWork
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _chatService.ProcesarMensajeAsync(request);

        // Assert
        Assert.True(result.Exitoso);
        Assert.Equal(1, result.IdConversacion);
        _mockConversationRepository.Verify(x => x.GetByIdAsync(1), Times.Once);
    }

    [Fact]
    public async Task ProcessMessageAsync_Should_Return_Error_When_AI_Provider_Fails()
    {
        // Arrange
        var request = new MensajeRequest
        {
            Mensaje = "Test",
            IdConversacion = null
        };

        var nuevaConversacion = new Conversacion
        {
            IdConversacion = 1,
            FechaInicio = DateTime.UtcNow,
            Estado = EstadoConversacion.Activa
        };

        _mockConversationRepository
            .Setup(x => x.CreateAsync(It.IsAny<Conversacion>()))
            .ReturnsAsync(nuevaConversacion);

        _mockConversationRepository
            .Setup(x => x.GetByIdAsync(It.IsAny<int>()))
            .ReturnsAsync(nuevaConversacion);

        _mockMessageRepository
            .Setup(x => x.CreateAsync(It.IsAny<Mensaje>()))
            .ReturnsAsync(new Mensaje());

        _mockOllamaService
            .Setup(x => x.SendMessageAsync(It.IsAny<IEnumerable<Mensaje>>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<double?>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Error en Ollama"));

        _mockUnitOfWork
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _chatService.ProcesarMensajeAsync(request);

        // Assert
        Assert.False(result.Exitoso);
        Assert.NotNull(result.Error);
        Assert.Contains("Error en Ollama", result.Error);
    }

    [Fact]
    public async Task ProcessMessageAsync_Should_Return_Error_When_Message_Is_Empty()
    {
        // Arrange
        var request = new MensajeRequest
        {
            Mensaje = "",
            IdConversacion = null
        };

        // Act
        var result = await _chatService.ProcesarMensajeAsync(request);

        // Assert
        Assert.False(result.Exitoso);
        Assert.NotNull(result.Error);
        Assert.Contains("vacío", result.Error);
    }

    [Fact]
    public async Task ProcessMessageAsync_Should_Create_New_Conversation_When_Conversation_Not_Found()
    {
        // Arrange
        var request = new MensajeRequest
        {
            Mensaje = "Test",
            IdConversacion = 999
        };

        var nuevaConversacion = new Conversacion
        {
            IdConversacion = 1,
            FechaInicio = DateTime.UtcNow,
            Estado = EstadoConversacion.Activa
        };

        _mockConversationRepository
            .Setup(x => x.GetByIdAsync(999))
            .ReturnsAsync((Conversacion?)null);

        _mockConversationRepository
            .Setup(x => x.CreateAsync(It.IsAny<Conversacion>()))
            .ReturnsAsync(nuevaConversacion);

        _mockConversationRepository
            .SetupSequence(x => x.GetByIdAsync(It.IsAny<int>()))
            .ReturnsAsync((Conversacion?)null)
            .ReturnsAsync(nuevaConversacion);

        _mockMessageRepository
            .Setup(x => x.CreateAsync(It.IsAny<Mensaje>()))
            .ReturnsAsync(new Mensaje());

        _mockOllamaService
            .Setup(x => x.SendMessageAsync(It.IsAny<IEnumerable<Mensaje>>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<double?>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Respuesta de prueba");

        _mockUnitOfWork
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _chatService.ProcesarMensajeAsync(request);

        // Assert
        Assert.True(result.Exitoso);
        _mockConversationRepository.Verify(x => x.CreateAsync(It.IsAny<Conversacion>()), Times.Once);
    }
}
