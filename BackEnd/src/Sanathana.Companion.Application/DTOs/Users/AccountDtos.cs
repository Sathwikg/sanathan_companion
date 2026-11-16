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
    public List<ChantSettingExport> ChantSettings { get; set; } = new();
    public List<CalendarMarkExport> CalendarMarks { get; set; } = new();
    public List<FeedbackExport> Feedback { get; set; } = new();
    public NotificationExport Notifications { get; set; } = new();

    /// <summary>Consents given and withdrawn, and the nominee (DPDP Act 2023, sections 6 and 14).</summary>
    public PrivacyExport Privacy { get; set; } = new();

    /// <summary>The phones registered for notifications. The push token itself is left out: it is a credential, not information about the seeker.</summary>
    public List<DeviceExport> Devices { get; set; } = new();

    /// <summary>The notifications this seeker was sent.</summary>
    public List<ReceivedNotificationExport> NotificationsReceived { get; set; } = new();

    public class PrivacyExport
    {
        public int? AcceptedNoticeVersion { get; set; }
        public DateTime? NoticeAcceptedAtUtc { get; set; }
        public DateTime? AdultConfirmedAtUtc { get; set; }
        public bool UsageAnalytics { get; set; }
        public string? NomineeName { get; set; }
        public string? NomineeRelationship { get; set; }
        public string? NomineeContact { get; set; }
        public List<ConsentExport> Consents { get; set; } = new();
    }

    public class ConsentExport
    {
        public string Purpose { get; set; } = string.Empty;
        public bool Granted { get; set; }
        public int NoticeVersion { get; set; }
        public DateTime AtUtc { get; set; }
        public string? Platform { get; set; }
    }

    public class DeviceExport
    {
        public string Platform { get; set; } = string.Empty;
        public string? DeviceModel { get; set; }
        public string? AppVersion { get; set; }
        public string? Language { get; set; }
        public string? TimeZone { get; set; }
        public DateTime RegisteredOnUtc { get; set; }
        public DateTime? LastNotifiedUtc { get; set; }
    }

    public class ReceivedNotificationExport
    {
        public string Title { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedUtc { get; set; }
        public DateTime? ReadAtUtc { get; set; }
    }

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

    /// <summary>A chant the seeker set their own mala size or vibration for.</summary>
    public class ChantSettingExport
    {
        public string Chant { get; set; } = string.Empty;
        public int? MalaSize { get; set; }
        public bool VibrationEnabled { get; set; }
    }

    public class CalendarMarkExport
    {
        public DateOnly Date { get; set; }
        public TimeOnly? Time { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Notes { get; set; }
        public string Kind { get; set; } = string.Empty;
        public bool RepeatYearly { get; set; }
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

/// <summary>Whether an account may be signed in to.</summary>
public class SetUserStatusDto
{
    public bool IsActive { get; set; }
}
