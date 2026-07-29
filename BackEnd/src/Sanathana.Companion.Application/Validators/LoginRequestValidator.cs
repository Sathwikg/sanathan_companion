using FluentValidation;
using Sanathana.Companion.Application.DTOs.Auth;

namespace Sanathana.Companion.Application.Validators;

public class LoginRequestValidator : AbstractValidator<LoginRequestDto>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Credential)
            .NotEmpty().WithMessage("Email or mobile number is required.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.");
    }
}
