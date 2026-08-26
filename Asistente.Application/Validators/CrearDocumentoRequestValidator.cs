using FluentValidation;
using Asistente.Shared;

namespace Asistente.Application.Validators;

public class CrearDocumentoRequestValidator : AbstractValidator<CrearDocumentoRequest>
{
    public CrearDocumentoRequestValidator()
    {
        RuleFor(x => x.Codigo)
            .NotEmpty().WithMessage("El código del documento es obligatorio.")
            .MaximumLength(50).WithMessage("El código no puede exceder los 50 caracteres.")
            .Matches(@"^[a-zA-Z0-9\-_]+$").WithMessage("El código solo puede contener letras, números, guiones y guiones bajos.");

        RuleFor(x => x.Nombre)
            .NotEmpty().WithMessage("El nombre del documento es obligatorio.")
            .MaximumLength(200).WithMessage("El nombre no puede exceder los 200 caracteres.");

        RuleFor(x => x.Descripcion)
            .MaximumLength(1000).WithMessage("La descripción no puede exceder los 1000 caracteres.");

        RuleFor(x => x.IdCategoria)
            .GreaterThan(0).WithMessage("Debe seleccionar una categoría válida.");
    }
}
