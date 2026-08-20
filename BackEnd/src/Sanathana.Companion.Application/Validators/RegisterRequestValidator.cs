using FluentValidation;
using Sanathana.Companion.Application.Common;
using Sanathana.Companion.Application.DTOs.Auth;
using Sanathana.Companion.Domain.Common;

namespace Sanathana.Companion.Application.Validators;

public class RegisterRequestValidator : AbstractValidator<RegisterRequestDto>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Full name is required.")
            .MaximumLength(150);

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Enter a valid email address.")
            .MaximumLength(256);

        RuleFor(x => x.MobileNumber)
            .NotEmpty().WithMessage("Mobile number is required.")
            .Matches(@"^[0-9+\-\s]{7,15}$").WithMessage("Enter a valid mobile number.")
            // The regex above accepts "+++++++" and "  -  -  ". Since the number is a login
            // credential and now carries a unique index, what matters is the digits it leaves.
            .Must(m => (CredentialNormalizer.Mobile(m)?.Length ?? 0) >= 10)
            .WithMessage("Enter a valid mobile number.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MustSatisfyPolicy();

        RuleFor(x => x)
            .Must(x => !ContainsIdentity(x, x.Password))
            .WithName(nameof(RegisterRequestDto.Password))
            .WithMessage("Your password must not contain your email address or mobile number.");

        RuleFor(x => x.ConfirmPassword)
            .Equal(x => x.Password).WithMessage("Passwords do not match.");

        RuleFor(x => x.SeekerName)
            .MaximumLength(150);
    }

    /// <summary>
    /// A password built out of the address or number typed two fields above is the first thing
    /// anyone guesses. The length floors matter: without them an address like m@example.com would
    /// reject every password containing the letter m.
    /// </summary>
    private static bool ContainsIdentity(RegisterRequestDto dto, string? password)
    {
        if (string.IsNullOrEmpty(password)) return false;

        var local = (dto.Email ?? string.Empty).Split('@')[0];
        if (local.Length >= 4 && password.Contains(local, StringComparison.OrdinalIgnoreCase)) return true;

        var digits = CredentialNormalizer.Mobile(dto.MobileNumber);
        return digits is { Length: >= 6 } && password.Contains(digits, StringComparison.Ordinal);
    }
}
