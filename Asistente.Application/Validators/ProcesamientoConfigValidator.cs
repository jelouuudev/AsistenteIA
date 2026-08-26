using Asistente.Shared;
using FluentValidation;

namespace Asistente.Application.Validators;

public class ProcesamientoConfigValidator : AbstractValidator<ProcesamientoConfig>
{
    public ProcesamientoConfigValidator()
    {
        RuleFor(x => x.TamanoMaximoChunk)
            .InclusiveBetween(200, 10000)
            .WithMessage("El tamaño máximo del chunk debe estar entre 200 y 10000 caracteres.");

        RuleFor(x => x.Solapamiento)
            .GreaterThanOrEqualTo(0)
            .WithMessage("El solapamiento no puede ser negativo.");

        RuleFor(x => x.Solapamiento)
            .LessThan(x => x.TamanoMaximoChunk / 2)
            .WithMessage("El solapamiento no puede ser mayor a la mitad del tamaño del chunk.");

        RuleFor(x => x.LongitudMinima)
            .InclusiveBetween(10, 1000)
            .WithMessage("La longitud mínima debe estar entre 10 y 1000 caracteres.");

        RuleFor(x => x.FrecuenciaSegundos)
            .InclusiveBetween(5, 3600)
            .WithMessage("La frecuencia debe estar entre 5 y 3600 segundos.");

        RuleFor(x => x.MaxDocumentosPorCiclo)
            .InclusiveBetween(1, 100)
            .WithMessage("El máximo de documentos por ciclo debe estar entre 1 y 100.");
    }
}
