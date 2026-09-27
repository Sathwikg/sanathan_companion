using FluentValidation;
using Sanathana.Companion.Application.Common;
using Sanathana.Companion.Application.DTOs.Auth;
using Sanathana.Companion.Domain.Common;

namespace Sanathana.Companion.Application.Validators;

/// <summary>
/// The rules every registration shares: name, mobile number, password and its confirmation, seeker
/// name. Abstract so FluentValidation's assembly scan does not try to register it on its own; the
/// two concrete validators add what differs: the email field on the plain form, the ticket on the
/// Google one.
/// </summary>
public abstract class RegistrationFieldsValidator<T> : AbstractValidator<T> where T : IRegistrationFields
{
    protected RegistrationFieldsValidator()
    {
        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Full name is required.")
            .MaximumLength(150);

        RuleFor(x => x.MobileNumber)
            .NotEmpty().WithMessage("Mobile number is required.")
            .Matches(@"^[0-9+\-\s]{7,15}$").WithMessage("Enter a valid mobile number.")
            // The regex above accepts "+++++++" and "  -  -  ". Since the number is a login
            // credential and carries a unique index, what matters is the digits it leaves.
            .Must(m => (CredentialNormalizer.Mobile(m)?.Length ?? 0) >= 10)
            .WithMessage("Enter a valid mobile number.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MustSatisfyPolicy();

        RuleFor(x => x.ConfirmPassword)
            .Equal(x => x.Password).WithMessage("Passwords do not match.");

        RuleFor(x => x.SeekerName)
            .MaximumLength(150);
    }
}
