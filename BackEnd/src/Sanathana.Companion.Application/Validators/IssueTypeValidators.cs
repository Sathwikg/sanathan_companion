using FluentValidation;
using Sanathana.Companion.Application.DTOs.IssueTypes;

namespace Sanathana.Companion.Application.Validators;

public class CreateIssueTypeValidator : AbstractValidator<CreateIssueTypeDto>
{
    public CreateIssueTypeValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Issue type name is required.").MaximumLength(150);
        RuleFor(x => x.Description).MaximumLength(500);
    }
}

public class UpdateIssueTypeValidator : AbstractValidator<UpdateIssueTypeDto>
{
    public UpdateIssueTypeValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Issue type name is required.").MaximumLength(150);
        RuleFor(x => x.Description).MaximumLength(500);
    }
}
