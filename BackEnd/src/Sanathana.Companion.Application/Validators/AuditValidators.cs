using FluentValidation;
using Sanathana.Companion.Application.Common;
using Sanathana.Companion.Application.DTOs.Audit;

namespace Sanathana.Companion.Application.Validators;

public class SaveAuditConfigValidator : AbstractValidator<SaveAuditConfigDto>
{
    public const int MinRetentionDays = 7;
    public const int MaxAuditRetentionDays = 730;
    public const int MaxErrorRetentionDays = 365;

    public SaveAuditConfigValidator()
    {
        RuleFor(x => x.Settings).NotNull();
        RuleFor(x => x.Settings.AuditRetentionDays)
            .InclusiveBetween(MinRetentionDays, MaxAuditRetentionDays)
            .WithMessage($"Audit log retention must be between {MinRetentionDays} and {MaxAuditRetentionDays} days.")
            .When(x => x.Settings is not null);
        RuleFor(x => x.Settings.ErrorRetentionDays)
            .InclusiveBetween(MinRetentionDays, MaxErrorRetentionDays)
            .WithMessage($"Error log retention must be between {MinRetentionDays} and {MaxErrorRetentionDays} days.")
            .When(x => x.Settings is not null);

        RuleFor(x => x.Modules).NotNull();
        RuleFor(x => x.Modules)
            .Must(m => m.Select(x => x.MenuModuleId).Distinct().Count() == m.Count)
            .WithMessage("Each form may appear only once.")
            .When(x => x.Modules is not null);
    }
}

/// <summary>
/// Only the shape is enforced here. Over-long text is trimmed by the service rather than rejected:
/// a crash report is worth keeping even when its stack trace is enormous.
/// </summary>
public class LogErrorRequestValidator : AbstractValidator<LogErrorRequestDto>
{
    public LogErrorRequestValidator()
    {
        RuleFor(x => x.Source)
            .Must(s => s is not null && AuditErrorSources.ClientReportable.Contains(s))
            .WithMessage("Source must be FrontendWeb or FrontendMobile.");
        RuleFor(x => x.Severity)
            .Must(s => s is not null && AuditSeverities.All.Contains(s))
            .WithMessage("Severity must be Critical, Error or Warning.");
        RuleFor(x => x.Message).NotEmpty();
        RuleFor(x => x.ExceptionType).MaximumLength(256);
        RuleFor(x => x.RequestPath).MaximumLength(300);
        RuleFor(x => x.StatusCode).InclusiveBetween(100, 599).When(x => x.StatusCode.HasValue);
    }
}

public class LogActivityRequestValidator : AbstractValidator<LogActivityRequestDto>
{
    public LogActivityRequestValidator()
    {
        RuleFor(x => x.RoutePath)
            .NotEmpty()
            .MaximumLength(300)
            .Must(p => p is not null && p.StartsWith('/'))
            .WithMessage("RoutePath must be an app-relative path starting with a slash.");
        RuleFor(x => x.TimeSpentSeconds).GreaterThanOrEqualTo(0);
    }
}
