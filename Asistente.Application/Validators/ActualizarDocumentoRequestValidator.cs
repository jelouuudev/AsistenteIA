using FluentValidation;
using Asistente.Shared;

namespace Asistente.Application.Validators;

public class ActualizarDocumentoRequestValidator : AbstractValidator<ActualizarDocumentoRequest>
{
    public ActualizarDocumentoRequestValidator()
    {
        RuleFor(x => x.Nombre)
            .NotEmpty().WithMessage("El nombre del documento es obligatorio.")
            .MaximumLength(200).WithMessage("El nombre no puede exceder los 200 caracteres.");

        RuleFor(x => x.Descripcion)
            .MaximumLength(1000).WithMessage("La descripción no puede exceder los 1000 caracteres.");

        RuleFor(x => x.IdCategoria)
            .GreaterThan(0).WithMessage("Debe seleccionar una categoría válida.");
    }
}
