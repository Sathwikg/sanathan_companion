using FluentValidation;
using Sanathana.Companion.Application.DTOs.Chants;

namespace Sanathana.Companion.Application.Validators;

public class CreateChantValidator : AbstractValidator<CreateChantDto>
{
    public CreateChantValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Chant name is required.").MaximumLength(150);
        RuleFor(x => x.Description).MaximumLength(1000);
        When(x => x.HasCount, () =>
        {
            RuleFor(x => x.Count).NotNull().WithMessage("Enter a count.")
                .GreaterThanOrEqualTo(1).WithMessage("Count must be at least 1.");
        });
    }
}

public class UpdateChantValidator : AbstractValidator<UpdateChantDto>
{
    public UpdateChantValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Chant name is required.").MaximumLength(150);
        RuleFor(x => x.Description).MaximumLength(1000);
        When(x => x.HasCount, () =>
        {
            RuleFor(x => x.Count).NotNull().WithMessage("Enter a count.")
                .GreaterThanOrEqualTo(1).WithMessage("Count must be at least 1.");
        });
    }
}
