using FluentValidation;
using Sanathana.Companion.Application.DTOs.Feedback;

namespace Sanathana.Companion.Application.Validators;

public class SubmitFeedbackValidator : AbstractValidator<SubmitFeedbackDto>
{
    public SubmitFeedbackValidator()
    {
        RuleFor(x => x.IssueTypeId).NotEmpty().WithMessage("Please choose an issue type.");
        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("Please describe your feedback.")
            .MaximumLength(2000);
    }
}
