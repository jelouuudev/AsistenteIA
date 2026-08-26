using Asistente.Shared;
using FluentValidation;

namespace Asistente.Application.Validators;

public class PruebaAsistenteRequestValidator : AbstractValidator<PruebaAsistenteRequest>
{
    public PruebaAsistenteRequestValidator()
    {
        RuleFor(x => x.IdAsistente)
            .GreaterThan(0).WithMessage("Debe seleccionar un asistente.");

        RuleFor(x => x.Mensaje)
            .NotEmpty().WithMessage("El mensaje de prueba es obligatorio.");
    }
}
