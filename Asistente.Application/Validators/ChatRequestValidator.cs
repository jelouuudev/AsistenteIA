using Asistente.Application.DTOs;
using FluentValidation;

namespace Asistente.Application.Validators;

/// <summary>
/// Validador para solicitudes de chat
/// </summary>
public class ChatRequestValidator : AbstractValidator<ChatRequestDto>
{
    public ChatRequestValidator()
    {
        RuleFor(x => x.Message)
            .NotEmpty().WithMessage("El mensaje no puede estar vacío.")
            .MinimumLength(1).WithMessage("El mensaje debe tener al menos 1 carácter.")
            .MaximumLength(4000).WithMessage("El mensaje no puede exceder 4000 caracteres.");

        RuleFor(x => x.ConversationId)
            .GreaterThan(0).When(x => x.ConversationId.HasValue)
            .WithMessage("El identificador de conversación debe ser mayor que 0.");
    }
}
