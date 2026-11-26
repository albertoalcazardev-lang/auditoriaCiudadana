using FluentValidation;

namespace VyTSystems.Application.Auth.Login;

public sealed class LoginValidator : AbstractValidator<LoginCommand>
{
    public LoginValidator()
    {
        RuleFor(x => x.Identifier)
            .NotEmpty().WithMessage("Ingresa tu usuario o correo electrónico.")
            .MaximumLength(256).WithMessage("El usuario o correo electrónico excede la longitud permitida.");
        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Ingresa tu contraseña.")
            .MaximumLength(4096).WithMessage("La contraseña excede la longitud permitida.");
        RuleFor(x => x.RequestedAccess)
            .IsInEnum().WithMessage("Selecciona un tipo de acceso válido.");
    }
}
