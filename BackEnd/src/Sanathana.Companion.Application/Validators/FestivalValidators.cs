using FluentValidation;
using Sanathana.Companion.Application.DTOs.Festivals;

namespace Sanathana.Companion.Application.Validators;

public class CreateFestivalValidator : AbstractValidator<CreateFestivalDto>
{
    public CreateFestivalValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Festival name is required.").MaximumLength(150);
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.Year).InclusiveBetween(1900, 2200);
        RuleFor(x => x.Date)
            .Must((dto, date) => date.Year == dto.Year)
            .WithMessage("The date must fall within the selected year.");
    }
}

public class UpdateFestivalValidator : AbstractValidator<UpdateFestivalDto>
{
    public UpdateFestivalValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Festival name is required.").MaximumLength(150);
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.Year).InclusiveBetween(1900, 2200);
        RuleFor(x => x.Date)
            .Must((dto, date) => date.Year == dto.Year)
            .WithMessage("The date must fall within the selected year.");
    }
}
