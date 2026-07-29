namespace Sanathana.Companion.Application.Common;

/// <summary>The triage states a feedback can be in.</summary>
public static class FeedbackStatuses
{
    public const string New = "New";
    public const string Reviewed = "Reviewed";
    public const string Resolved = "Resolved";

    public static readonly string[] All = { New, Reviewed, Resolved };

    public static bool IsValid(string? status)
        => status is not null && All.Contains(status, StringComparer.OrdinalIgnoreCase);
}
