using Asistente.Shared;
using FluentValidation;

namespace Asistente.Application.Validators;

public class CrearAsistenteRequestValidator : AbstractValidator<CrearAsistenteRequest>
{
    public CrearAsistenteRequestValidator()
    {
        RuleFor(x => x.Nombre)
            .NotEmpty().WithMessage("El nombre del asistente es obligatorio.")
            .MaximumLength(100).WithMessage("El nombre no puede exceder 100 caracteres.");

        RuleFor(x => x.ModeloIA)
            .NotEmpty().WithMessage("El modelo IA es obligatorio.")
            .MaximumLength(100).WithMessage("El modelo no puede exceder 100 caracteres.");
    }
}
