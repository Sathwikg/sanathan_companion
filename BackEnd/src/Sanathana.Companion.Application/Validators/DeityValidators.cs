using FluentValidation;
using Sanathana.Companion.Application.DTOs.Deities;

namespace Sanathana.Companion.Application.Validators;

public class CreateDeityValidator : AbstractValidator<CreateDeityDto>
{
    public CreateDeityValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("God name is required.").MaximumLength(150);
        RuleFor(x => x.Description).MaximumLength(1000);
        RuleFor(x => x.WelcomeNote).MaximumLength(1000);
        RuleFor(x => x.DeityType).Must(t => t is "God" or "Goddess").WithMessage("Select God or Goddess.");
        RuleFor(x => x.ImageBase64).MaximumLength(1_500_000).WithMessage("The image is too large.");
    }
}

public class UpdateDeityValidator : AbstractValidator<UpdateDeityDto>
{
    public UpdateDeityValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("God name is required.").MaximumLength(150);
        RuleFor(x => x.Description).MaximumLength(1000);
        RuleFor(x => x.WelcomeNote).MaximumLength(1000);
        RuleFor(x => x.DeityType).Must(t => t is "God" or "Goddess").WithMessage("Select God or Goddess.");
        RuleFor(x => x.ImageBase64).MaximumLength(1_500_000).WithMessage("The image is too large.");
    }
}
