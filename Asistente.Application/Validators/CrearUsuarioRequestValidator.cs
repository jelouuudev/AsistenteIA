using FluentValidation;
using Asistente.Shared;

namespace Asistente.Application.Validators;

public class CrearUsuarioRequestValidator : AbstractValidator<CrearUsuarioRequest>
{
    public CrearUsuarioRequestValidator()
    {
        RuleFor(x => x.UsuarioNombre)
            .NotEmpty().WithMessage("El nombre de usuario es obligatorio.")
            .MaximumLength(50).WithMessage("El nombre de usuario no puede exceder los 50 caracteres.");

        RuleFor(x => x.Nombres)
            .NotEmpty().WithMessage("El nombre es obligatorio.")
            .MaximumLength(100).WithMessage("El nombre no puede exceder los 100 caracteres.");

        RuleFor(x => x.Apellidos)
            .NotEmpty().WithMessage("El apellido es obligatorio.")
            .MaximumLength(100).WithMessage("El apellido no puede exceder los 100 caracteres.");

        RuleFor(x => x.Correo)
            .NotEmpty().WithMessage("El correo electrónico es obligatorio.")
            .EmailAddress().WithMessage("El correo electrónico no es válido.")
            .MaximumLength(100).WithMessage("El correo electrónico no puede exceder los 100 caracteres.");

        RuleFor(x => x.Contrasena)
            .NotEmpty().WithMessage("La contraseña es obligatoria.")
            .MinimumLength(6).WithMessage("La contraseña debe tener al menos 6 caracteres.");
    }
}
