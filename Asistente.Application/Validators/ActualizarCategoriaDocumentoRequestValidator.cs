using FluentValidation;
using Asistente.Shared;

namespace Asistente.Application.Validators;

public class ActualizarCategoriaDocumentoRequestValidator : AbstractValidator<ActualizarCategoriaDocumentoRequest>
{
    public ActualizarCategoriaDocumentoRequestValidator()
    {
        RuleFor(x => x.Nombre)
            .NotEmpty().WithMessage("El nombre de la categoría es obligatorio.")
            .MaximumLength(100).WithMessage("El nombre no puede exceder los 100 caracteres.");

        RuleFor(x => x.Descripcion)
            .MaximumLength(500).WithMessage("La descripción no puede exceder los 500 caracteres.");
    }
}
