using FluentValidation;
using Asistente.Shared;

namespace Asistente.Application.Validators;

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Usuario)
            .NotEmpty().WithMessage("El nombre de usuario es obligatorio.")
            .MaximumLength(50).WithMessage("El nombre de usuario no puede exceder los 50 caracteres.");

        RuleFor(x => x.Contrasena)
            .NotEmpty().WithMessage("La contraseña es obligatoria.");
    }
}
