using Asistente.Shared;
using FluentValidation;

namespace Asistente.Application.Validators;

public class CrearPromptRequestValidator : AbstractValidator<CrearPromptRequest>
{
    public CrearPromptRequestValidator()
    {
        RuleFor(x => x.IdAsistente)
            .GreaterThan(0).WithMessage("Debe seleccionar un asistente.");

        RuleFor(x => x.Nombre)
            .NotEmpty().WithMessage("El nombre del prompt es obligatorio.")
            .MaximumLength(200).WithMessage("El nombre no puede exceder 200 caracteres.");

        RuleFor(x => x.Contenido)
            .NotEmpty().WithMessage("El contenido del prompt es obligatorio.");
    }
}
