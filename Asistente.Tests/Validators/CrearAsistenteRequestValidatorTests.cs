using Asistente.Application.Validators;
using Asistente.Shared;
using Xunit;

namespace Asistente.Tests.Validators;

public class CrearAsistenteRequestValidatorTests
{
    private readonly CrearAsistenteRequestValidator _validator;

    public CrearAsistenteRequestValidatorTests()
    {
        _validator = new CrearAsistenteRequestValidator();
    }

    [Fact]
    public void Should_Have_Error_When_Nombre_Is_Empty()
    {
        var request = new CrearAsistenteRequest { Nombre = "" };
        var result = _validator.Validate(request);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Should_Have_Error_When_ModeloIA_Is_Empty()
    {
        var request = new CrearAsistenteRequest { Nombre = "Test", ModeloIA = "" };
        var result = _validator.Validate(request);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Should_Not_Have_Error_When_Request_Is_Valid()
    {
        var request = new CrearAsistenteRequest
        {
            Nombre = "Asistente Comercial",
            ModeloIA = "qwen2.5:7b"
        };
        var result = _validator.Validate(request);
        Assert.True(result.IsValid);
    }
}
