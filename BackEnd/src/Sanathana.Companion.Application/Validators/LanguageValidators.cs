using FluentValidation;
using Sanathana.Companion.Application.DTOs.Languages;

namespace Sanathana.Companion.Application.Validators;

public class CreateLanguageValidator : AbstractValidator<CreateLanguageDto>
{
    public CreateLanguageValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Language name is required.").MaximumLength(150);
        RuleFor(x => x.NativeName).MaximumLength(150);
        RuleFor(x => x.Code).MaximumLength(10);
        RuleFor(x => x.Description).MaximumLength(500);
    }
}

public class UpdateLanguageValidator : AbstractValidator<UpdateLanguageDto>
{
    public UpdateLanguageValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Language name is required.").MaximumLength(150);
        RuleFor(x => x.NativeName).MaximumLength(150);
        RuleFor(x => x.Code).MaximumLength(10);
        RuleFor(x => x.Description).MaximumLength(500);
    }
}
