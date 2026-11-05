using FluentValidation;
using Sanathana.Companion.Application.DTOs.Calendar;
using Sanathana.Companion.Domain.Entities;

namespace Sanathana.Companion.Application.Validators;

public class SaveCalendarEventValidator : AbstractValidator<SaveCalendarEventDto>
{
    public SaveCalendarEventValidator()
    {
        RuleFor(x => x.Title).NotEmpty().WithMessage("Give the mark a title.").MaximumLength(CalendarEvent.MaxTitleLength);
        RuleFor(x => x.Notes).MaximumLength(CalendarEvent.MaxNotesLength);
        RuleFor(x => x.Kind)
            .Must(k => CalendarEventKinds.Normalize(k) is not null)
            .WithMessage("Unknown kind of mark.");
        RuleFor(x => x.Date.Year).InclusiveBetween(1900, 2100).WithMessage("Date must be between 1900 and 2100.");
    }
}
