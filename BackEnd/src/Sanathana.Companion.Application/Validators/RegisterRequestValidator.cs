using FluentValidation;
using Sanathana.Companion.Application.Common;
using Sanathana.Companion.Application.DTOs.Auth;
using Sanathana.Companion.Domain.Common;

namespace Sanathana.Companion.Application.Validators;

public class RegisterRequestValidator : RegistrationFieldsValidator<RegisterRequestDto>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Enter a valid email address.")
            .MaximumLength(256);

        // The Google form cannot run this rule here: its email arrives in the ticket, so the
        // service applies the same check after reading it.
        RuleFor(x => x)
            .Must(x => !IdentityInPassword.Contains(x.Email, CredentialNormalizer.Mobile(x.MobileNumber), x.Password))
            .WithName(nameof(RegisterRequestDto.Password))
            .WithMessage("Your password must not contain your email address or mobile number.");
    }
}
