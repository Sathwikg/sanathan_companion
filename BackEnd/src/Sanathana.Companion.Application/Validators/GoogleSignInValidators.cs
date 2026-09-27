using FluentValidation;
using Sanathana.Companion.Application.DTOs.Auth;

namespace Sanathana.Companion.Application.Validators;

public class GoogleRegisterValidator : RegistrationFieldsValidator<GoogleRegisterDto>
{
    public GoogleRegisterValidator()
    {
        RuleFor(x => x.Ticket)
            .NotEmpty().WithMessage("Your Google sign-in has expired. Please try again.");
    }
}

public class GoogleLinkValidator : AbstractValidator<GoogleLinkDto>
{
    public GoogleLinkValidator()
    {
        RuleFor(x => x.Ticket)
            .NotEmpty().WithMessage("Your Google sign-in has expired. Please try again.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Enter the password for this account.");
    }
}
