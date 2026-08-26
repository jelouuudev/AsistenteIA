using Asistente.Shared;
using FluentValidation;

namespace Asistente.Application.Validators;

public class ActualizarPromptRequestValidator : AbstractValidator<ActualizarPromptRequest>
{
    public ActualizarPromptRequestValidator()
    {
        RuleFor(x => x.Nombre)
            .NotEmpty().WithMessage("El nombre del prompt es obligatorio.")
            .MaximumLength(200).WithMessage("El nombre no puede exceder 200 caracteres.");

        RuleFor(x => x.Contenido)
            .NotEmpty().WithMessage("El contenido del prompt es obligatorio.");
    }
}
