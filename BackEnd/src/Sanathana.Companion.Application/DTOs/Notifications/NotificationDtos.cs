namespace Sanathana.Companion.Application.DTOs.Notifications;

// ---------- Admin: which notification types exist, and how each behaves ----------

/// <summary>
/// One notification type on one form, with its configuration (if any). Every form can have the
/// plain daily reminder; the forms the server knows more about also list their built-in types.
/// </summary>
public class NotificationModuleDto
{
    public Guid MenuModuleId { get; set; }

    /// <summary>The type's code: "reminder" for the plain daily reminder, else a built-in type.</summary>
    public string Kind { get; set; } = "reminder";

    /// <summary>The type's own English name ("Rahu Kalam Warning"), for the admin's list.</summary>
    public string KindName { get; set; } = string.Empty;

    /// <summary>What the type does, in a sentence, so the admin need not guess from its name.</summary>
    public string? KindDescription { get; set; }

    /// <summary>"Device", "UserTime", "DailyAt", "BeforeWindow" or "Manual": which of the numbers below apply.</summary>
    public string Schedule { get; set; } = "Device";

    /// <summary>The send time in force when <see cref="SendTime"/> is left empty.</summary>
    public TimeOnly? DefaultSendTime { get; set; }
    public int? DefaultLeadMinutes { get; set; }
    public int? DefaultThreshold { get; set; }

    public TimeOnly? SendTime { get; set; }
    public int? LeadMinutes { get; set; }
    public int? Threshold { get; set; }
    public string? ImageUrl { get; set; }
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

    /// <summary>Omitted by older clients, which only knew the plain reminder.</summary>
    public string Kind { get; set; } = "reminder";

    public TimeOnly? SendTime { get; set; }
    public int? LeadMinutes { get; set; }
    public int? Threshold { get; set; }
    public string? ImageUrl { get; set; }
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

    /// <summary>The type's code; the app translates a built-in type's name from "push.{Kind}.name".</summary>
    public string Kind { get; set; } = "reminder";

    /// <summary>"Device", "UserTime", "DailyAt", "BeforeWindow" or "Manual".</summary>
    public string Schedule { get; set; } = "Device";

    /// <summary>
    /// True when an administrator typed the title or description, which is then shown as typed.
    /// False means the built-in wording, which the app shows in the seeker's language.
    /// </summary>
    public bool HasCustomText { get; set; }

    /// <summary>For a type the seeker times themselves: when it goes out until they pick a time.</summary>
    public TimeOnly? DefaultTime { get; set; }

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

    /// <summary>
    /// Whether the server is able to send push notifications at all. When it is not, the phone
    /// keeps its own alarms for the daily reminders, as it did before push existed.
    /// </summary>
    public bool PushEnabled { get; set; }

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

// ---------- User: this phone, and what has arrived ----------

/// <summary>A phone introducing itself so notifications can reach it.</summary>
public class RegisterDeviceDto
{
    /// <summary>1 Android, 2 iOS (PushPlatform).</summary>
    public int Platform { get; set; }

    public string Token { get; set; } = string.Empty;
    public string? DeviceModel { get; set; }
    public string? AppVersion { get; set; }

    /// <summary>IANA time zone id of the device ("Asia/Kolkata").</summary>
    public string? TimeZone { get; set; }

    /// <summary>The language the app is showing ("te").</summary>
    public string? Language { get; set; }
}

public class RemoveDeviceDto
{
    public string Token { get; set; } = string.Empty;
}

/// <summary>One notification the seeker was sent.</summary>
public class InboxItemDto
{
    public Guid Id { get; set; }
    public string Kind { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string? Route { get; set; }
    public DateTime SentAtUtc { get; set; }
    public bool IsRead { get; set; }
}

public class InboxDto
{
    public int Unread { get; set; }
    public List<InboxItemDto> Items { get; set; } = new();
}
