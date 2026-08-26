using Asistente.Shared;
using FluentValidation;

namespace Asistente.Application.Validators;

public class ActualizarConfiguracionMemoriaRequestValidator : AbstractValidator<ActualizarConfiguracionMemoriaRequest>
{
    public ActualizarConfiguracionMemoriaRequestValidator()
    {
        RuleFor(x => x.MaximoMensajesContexto)
            .InclusiveBetween(1, 200)
            .WithMessage("El máximo de mensajes debe estar entre 1 y 200.");

        RuleFor(x => x.MaximoTokensContexto)
            .InclusiveBetween(256, 64000)
            .WithMessage("El máximo de tokens debe estar entre 256 y 64000.");

        RuleFor(x => x.LongitudResumen)
            .InclusiveBetween(50, 5000)
            .WithMessage("La longitud del resumen debe estar entre 50 y 5000 caracteres.");

        RuleFor(x => x.CantidadConversacionesVisibles)
            .InclusiveBetween(5, 200)
            .WithMessage("La cantidad de conversaciones visibles debe estar entre 5 y 200.");
    }
}

public class RenombrarConversacionRequestValidator : AbstractValidator<RenombrarConversacionRequest>
{
    public RenombrarConversacionRequestValidator()
    {
        RuleFor(x => x.Titulo)
            .NotEmpty()
            .WithMessage("El título no puede estar vacío.")
            .MaximumLength(200)
            .WithMessage("El título no puede exceder los 200 caracteres.");
    }
}
