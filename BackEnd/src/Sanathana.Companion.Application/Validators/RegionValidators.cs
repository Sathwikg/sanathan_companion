using FluentValidation;
using Sanathana.Companion.Application.DTOs.Regions;

namespace Sanathana.Companion.Application.Validators;

public class CreateRegionValidator : AbstractValidator<CreateRegionDto>
{
    public CreateRegionValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Region name is required.").MaximumLength(150);
        RuleFor(x => x.Description).MaximumLength(500);
    }
}

public class UpdateRegionValidator : AbstractValidator<UpdateRegionDto>
{
    public UpdateRegionValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Region name is required.").MaximumLength(150);
        RuleFor(x => x.Description).MaximumLength(500);
    }
}
