using FluentValidation;
using Sanathana.Companion.Application.DTOs.Auth;

namespace Sanathana.Companion.Application.Validators;

public class ChangePasswordValidator : AbstractValidator<ChangePasswordDto>
{
    public ChangePasswordValidator()
    {
        RuleFor(x => x.CurrentPassword)
            .NotEmpty().WithMessage("Enter your current password.");

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("Enter a new password.")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters.")
            .Must(p => p.Any(char.IsLetter) && p.Any(c => !char.IsLetter(c)))
            .WithMessage("Password must contain at least one letter and one number or symbol.");

        RuleFor(x => x.ConfirmNewPassword)
            .Equal(x => x.NewPassword).WithMessage("The passwords do not match.");

        RuleFor(x => x.NewPassword)
            .NotEqual(x => x.CurrentPassword).WithMessage("The new password must be different.");
    }
}
