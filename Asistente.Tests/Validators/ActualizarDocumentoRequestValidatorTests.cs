using Asistente.Application.Validators;
using Asistente.Shared;
using FluentValidation.TestHelper;
using Xunit;

namespace Asistente.Tests.Validators;

public class ActualizarDocumentoRequestValidatorTests
{
    private readonly ActualizarDocumentoRequestValidator _validator = new();

    private static ActualizarDocumentoRequest CreateValidRequest()
    {
        return new ActualizarDocumentoRequest
        {
            Nombre = "Manual de Usuario Actualizado",
            Descripcion = "Descripción actualizada",
            IdCategoria = 1
        };
    }

    [Fact]
    public async Task Should_Not_Have_Error_When_Request_Is_Valid()
    {
        var request = CreateValidRequest();
        var result = await _validator.TestValidateAsync(request);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public async Task Should_Have_Error_When_Nombre_Is_Empty()
    {
        var request = CreateValidRequest();
        request.Nombre = "";
        var result = await _validator.TestValidateAsync(request);
        result.ShouldHaveValidationErrorFor(x => x.Nombre);
    }

    [Fact]
    public async Task Should_Have_Error_When_Nombre_Exceeds_MaxLength()
    {
        var request = CreateValidRequest();
        request.Nombre = new string('A', 201);
        var result = await _validator.TestValidateAsync(request);
        result.ShouldHaveValidationErrorFor(x => x.Nombre);
    }

    [Fact]
    public async Task Should_Have_Error_When_Descripcion_Exceeds_MaxLength()
    {
        var request = CreateValidRequest();
        request.Descripcion = new string('A', 1001);
        var result = await _validator.TestValidateAsync(request);
        result.ShouldHaveValidationErrorFor(x => x.Descripcion);
    }

    [Fact]
    public async Task Should_Have_Error_When_IdCategoria_Is_Zero()
    {
        var request = CreateValidRequest();
        request.IdCategoria = 0;
        var result = await _validator.TestValidateAsync(request);
        result.ShouldHaveValidationErrorFor(x => x.IdCategoria);
    }

    [Fact]
    public async Task Should_Have_Error_When_IdCategoria_Is_Negative()
    {
        var request = CreateValidRequest();
        request.IdCategoria = -1;
        var result = await _validator.TestValidateAsync(request);
        result.ShouldHaveValidationErrorFor(x => x.IdCategoria);
    }

    [Fact]
    public async Task Should_Not_Have_Error_When_Descripcion_Is_Null()
    {
        var request = CreateValidRequest();
        request.Descripcion = null;
        var result = await _validator.TestValidateAsync(request);
        result.ShouldNotHaveValidationErrorFor(x => x.Descripcion);
    }
}
