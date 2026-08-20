namespace Sanathana.Companion.Application.DTOs.Users;

/// <summary>Confirms deletion of the caller's own account.</summary>
/// <remarks>
/// The password is re-checked because this is irreversible and a borrowed or stolen session must
/// not be enough to erase somebody's practice history.
/// </remarks>
public class DeleteAccountDto
{
    public string Password { get; set; } = string.Empty;
}

/// <summary>Everything the app holds about one user, for them to take away.</summary>
public class MyDataExportDto
{
    public DateTime ExportedAtUtc { get; set; }
    public AccountExport Account { get; set; } = new();
    public List<SadhanaEntryExport> Sadhana { get; set; } = new();
    public List<FavoriteExport> Favorites { get; set; } = new();
    public List<FeedbackExport> Feedback { get; set; } = new();
    public NotificationExport Notifications { get; set; } = new();

    public class AccountExport
    {
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string MobileNumber { get; set; } = string.Empty;
        public string? SeekerName { get; set; }
        public string Role { get; set; } = string.Empty;
        public string? DefaultRegion { get; set; }
        public DateTime RegisteredOn { get; set; }
    }

    public class SadhanaEntryExport
    {
        public DateOnly Date { get; set; }
        public string Chant { get; set; } = string.Empty;
        public int MalasCompleted { get; set; }
        public int TotalCount { get; set; }
    }

    public class FavoriteExport
    {
        public string Type { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }

    public class FeedbackExport
    {
        public DateTime SubmittedOn { get; set; }
        public string IssueType { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }

    public class NotificationExport
    {
        public bool MasterEnabled { get; set; }
        public bool QuietHoursEnabled { get; set; }
        public TimeOnly? QuietFrom { get; set; }
        public TimeOnly? QuietTo { get; set; }
        public List<PreferenceExport> Preferences { get; set; } = new();
    }

    public class PreferenceExport
    {
        public string Notification { get; set; } = string.Empty;
        public bool IsEnabled { get; set; }
        public TimeOnly? FromTime { get; set; }
        public TimeOnly? ToTime { get; set; }
    }
}
