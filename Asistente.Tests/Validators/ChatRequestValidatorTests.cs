using Asistente.Application.DTOs;
using Asistente.Application.Validators;
using FluentValidation;
using Xunit;

namespace Asistente.Tests.Validators;

public class ChatRequestValidatorTests
{
    private readonly ChatRequestValidator _validator;

    public ChatRequestValidatorTests()
    {
        _validator = new ChatRequestValidator();
    }

    [Fact]
    public void Should_Have_Error_When_Message_Is_Empty()
    {
        // Arrange
        var request = new ChatRequestDto { Message = "" };

        // Act
        var result = _validator.Validate(request);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Message");
    }

    [Fact]
    public void Should_Have_Error_When_Message_Is_Null()
    {
        // Arrange
        var request = new ChatRequestDto { Message = null! };

        // Act
        var result = _validator.Validate(request);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Message");
    }

    [Fact]
    public void Should_Have_Error_When_Message_Exceeds_Maximum_Length()
    {
        // Arrange
        var request = new ChatRequestDto { Message = new string('a', 4001) };

        // Act
        var result = _validator.Validate(request);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Message");
    }

    [Fact]
    public void Should_Not_Have_Error_When_Message_Is_Valid()
    {
        // Arrange
        var request = new ChatRequestDto { Message = "Hola, ¿cómo estás?" };

        // Act
        var result = _validator.Validate(request);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Should_Have_Error_When_ConversationId_Is_Zero()
    {
        // Arrange
        var request = new ChatRequestDto { Message = "Test", ConversationId = 0 };

        // Act
        var result = _validator.Validate(request);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "ConversationId");
    }

    [Fact]
    public void Should_Not_Have_Error_When_ConversationId_Is_Null()
    {
        // Arrange
        var request = new ChatRequestDto { Message = "Test", ConversationId = null };

        // Act
        var result = _validator.Validate(request);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Should_Not_Have_Error_When_ConversationId_Is_Valid()
    {
        // Arrange
        var request = new ChatRequestDto { Message = "Test", ConversationId = 1 };

        // Act
        var result = _validator.Validate(request);

        // Assert
        Assert.True(result.IsValid);
    }
}
