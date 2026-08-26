using Asistente.Application.Validators;
using Asistente.Shared;
using Xunit;

namespace Asistente.Tests.Validators;

public class CrearPromptRequestValidatorTests
{
    private readonly CrearPromptRequestValidator _validator;

    public CrearPromptRequestValidatorTests()
    {
        _validator = new CrearPromptRequestValidator();
    }

    [Fact]
    public void Should_Have_Error_When_IdAsistente_Is_Zero()
    {
        var request = new CrearPromptRequest { IdAsistente = 0, Nombre = "Test", Contenido = "Content" };
        var result = _validator.Validate(request);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Should_Have_Error_When_Nombre_Is_Empty()
    {
        var request = new CrearPromptRequest { IdAsistente = 1, Nombre = "", Contenido = "Content" };
        var result = _validator.Validate(request);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Should_Have_Error_When_Contenido_Is_Empty()
    {
        var request = new CrearPromptRequest { IdAsistente = 1, Nombre = "Test", Contenido = "" };
        var result = _validator.Validate(request);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Should_Not_Have_Error_When_Request_Is_Valid()
    {
        var request = new CrearPromptRequest
        {
            IdAsistente = 1,
            Nombre = "Prompt principal",
            Contenido = "Eres un asistente..."
        };
        var result = _validator.Validate(request);
        Assert.True(result.IsValid);
    }
}
