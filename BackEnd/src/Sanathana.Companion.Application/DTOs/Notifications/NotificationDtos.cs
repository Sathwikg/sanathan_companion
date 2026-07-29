namespace Sanathana.Companion.Application.DTOs.Notifications;

// ---------- Admin: which modules may notify ----------

/// <summary>One selectable form, with its notification configuration (if any).</summary>
public class NotificationModuleDto
{
    public Guid MenuModuleId { get; set; }
    public string ModuleName { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public string? ParentName { get; set; }

    /// <summary>True when this module is configured to send notifications.</summary>
    public bool IsEnabled { get; set; }

    public string? Title { get; set; }
    public string? Description { get; set; }
    public bool DefaultEnabledForUsers { get; set; } = true;
    public bool IsMandatory { get; set; }
    public int DisplayOrder { get; set; }

    /// <summary>How many users have explicitly opted out — helps the admin judge impact.</summary>
    public int OptedOutUsers { get; set; }
}

public class NotificationConfigListDto
{
    public List<NotificationModuleDto> Modules { get; set; } = new();
}

/// <summary>One row as posted back on save.</summary>
public class SaveNotificationModuleDto
{
    public Guid MenuModuleId { get; set; }
    public bool IsEnabled { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public bool DefaultEnabledForUsers { get; set; } = true;
    public bool IsMandatory { get; set; }
    public int DisplayOrder { get; set; }
}

public class SaveNotificationConfigDto
{
    public List<SaveNotificationModuleDto> Items { get; set; } = new();
}

// ---------- User: what I get, and when ----------

/// <summary>One notification type as the user sees it.</summary>
public class MyNotificationItemDto
{
    public Guid ConfigId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Icon { get; set; }
    public string ModuleName { get; set; } = string.Empty;

    /// <summary>Cannot be switched off by the user.</summary>
    public bool IsMandatory { get; set; }

    /// <summary>The user's effective choice (their saved preference, or the admin default).</summary>
    public bool IsEnabled { get; set; }

    /// <summary>Optional preferred window for this notification.</summary>
    public TimeOnly? FromTime { get; set; }
    public TimeOnly? ToTime { get; set; }

    /// <summary>Whether this would be delivered right now (respects quiet hours + window).</summary>
    public bool IsActiveNow { get; set; }

    /// <summary>Why it wouldn't fire right now — shown as a hint.</summary>
    public string? InactiveReason { get; set; }
}

public class MyNotificationSettingsDto
{
    public bool MasterEnabled { get; set; } = true;

    public bool QuietHoursEnabled { get; set; }
    public TimeOnly? QuietFrom { get; set; }
    public TimeOnly? QuietTo { get; set; }

    /// <summary>Server time (IST) the "active now" flags were computed against.</summary>
    public TimeOnly CurrentTime { get; set; }

    /// <summary>True while quiet hours are in effect.</summary>
    public bool InQuietHoursNow { get; set; }

    public List<MyNotificationItemDto> Items { get; set; } = new();
}

public class SaveMyNotificationItemDto
{
    public Guid ConfigId { get; set; }
    public bool IsEnabled { get; set; }
    public TimeOnly? FromTime { get; set; }
    public TimeOnly? ToTime { get; set; }
}

public class SaveMyNotificationSettingsDto
{
    public bool MasterEnabled { get; set; } = true;
    public bool QuietHoursEnabled { get; set; }
    public TimeOnly? QuietFrom { get; set; }
    public TimeOnly? QuietTo { get; set; }
    public List<SaveMyNotificationItemDto> Items { get; set; } = new();
}
