using FluentValidation;
using Sanathana.Companion.Application.DTOs.Menu;

namespace Sanathana.Companion.Application.Validators;

public class CreateMenuModuleValidator : AbstractValidator<CreateMenuModuleDto>
{
    public CreateMenuModuleValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Name is required.").MaximumLength(150);
        RuleFor(x => x.Icon).MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.RoutePath).MaximumLength(300);
        RuleFor(x => x.DisplayOrder).GreaterThanOrEqualTo(0);
    }
}

public class UpdateMenuModuleValidator : AbstractValidator<UpdateMenuModuleDto>
{
    public UpdateMenuModuleValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Name is required.").MaximumLength(150);
        RuleFor(x => x.Icon).MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.RoutePath).MaximumLength(300);
        RuleFor(x => x.DisplayOrder).GreaterThanOrEqualTo(0);
    }
}
