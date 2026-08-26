using Asistente.Domain.Interfaces;
using Asistente.Infrastructure.Services;
using Asistente.Shared;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Xunit;

namespace Asistente.Tests.Services;

public class OllamaProviderTests
{
    private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;
    private readonly HttpClient _httpClient;
    private readonly Mock<ILogger<OllamaProvider>> _mockLogger;
    private readonly IOptions<OllamaConfig> _config;
    private readonly IAIProvider _ollamaProvider;

    public OllamaProviderTests()
    {
        _mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_mockHttpMessageHandler.Object)
        {
            BaseAddress = new Uri("http://localhost:11434")
        };
        _mockLogger = new Mock<ILogger<OllamaProvider>>();
        _config = Options.Create(new OllamaConfig
        {
            Url = "http://localhost:11434",
            Modelo = "deepseek-r1:7b",
            TimeoutSegundos = 300
        });

        _ollamaProvider = new OllamaProvider(_httpClient, _config, _mockLogger.Object);
    }

    [Fact]
    public async Task SendMessageAsync_Should_Return_Response_When_Request_Is_Successful()
    {
        // Arrange
        var messages = new List<string> { "Hola", "¡Hola! ¿Cómo estás?" };
        var responseContent = new
        {
            message = new
            {
                role = "assistant",
                content = "Estoy bien, gracias por preguntar."
            }
        };

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(responseContent), Encoding.UTF8, "application/json")
            });

        // Act
        var result = await _ollamaProvider.SendMessageAsync(messages);

        // Assert
        Assert.Equal("Estoy bien, gracias por preguntar.", result);
    }

    [Fact]
    public async Task SendMessageAsync_Should_Throw_InvalidOperationException_When_Ollama_Is_Not_Available()
    {
        // Arrange
        var messages = new List<string> { "Hola" };

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("No se puede conectar"));

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => 
            _ollamaProvider.SendMessageAsync(messages));
    }

    [Fact]
    public async Task SendMessageAsync_Should_Throw_TimeoutException_When_Request_Times_Out()
    {
        // Arrange
        var messages = new List<string> { "Hola" };

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new TaskCanceledException());

        // Act & Assert
        await Assert.ThrowsAsync<TimeoutException>(() => 
            _ollamaProvider.SendMessageAsync(messages));
    }

    [Fact]
    public async Task SendMessageAsync_Should_Throw_InvalidOperationException_When_Model_Not_Found()
    {
        // Arrange
        var messages = new List<string> { "Hola" };

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.NotFound
            });

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => 
            _ollamaProvider.SendMessageAsync(messages));
        Assert.Contains("no está disponible", exception.Message);
    }

    [Fact]
    public async Task SendMessageAsync_Should_Throw_InvalidOperationException_When_Response_Is_Invalid()
    {
        // Arrange
        var messages = new List<string> { "Hola" };

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("invalid json", Encoding.UTF8, "application/json")
            });

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => 
            _ollamaProvider.SendMessageAsync(messages));
    }
}
