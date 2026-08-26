using FluentValidation;
using Asistente.Shared;

namespace Asistente.Application.Validators;

public class CrearRolRequestValidator : AbstractValidator<CrearRolRequest>
{
    public CrearRolRequestValidator()
    {
        RuleFor(x => x.Nombre)
            .NotEmpty().WithMessage("El nombre del rol es obligatorio.")
            .MaximumLength(50).WithMessage("El nombre del rol no puede exceder los 50 caracteres.");

        RuleFor(x => x.Descripcion)
            .MaximumLength(250).WithMessage("La descripción del rol no puede exceder los 250 caracteres.");
    }
}
