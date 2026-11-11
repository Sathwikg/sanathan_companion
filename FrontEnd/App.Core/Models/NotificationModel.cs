namespace App.Core.Models;

// ---------- Admin ----------

public class NotificationModuleModel
{
    public Guid MenuModuleId { get; set; }

    /// <summary>"reminder" for the plain daily reminder, else a built-in type's code.</summary>
    public string Kind { get; set; } = "reminder";
    public string KindName { get; set; } = string.Empty;
    public string? KindDescription { get; set; }

    /// <summary>"Device", "UserTime", "DailyAt", "BeforeWindow" or "Manual": which numbers apply.</summary>
    public string Schedule { get; set; } = "Device";
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

    public bool IsEnabled { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public bool DefaultEnabledForUsers { get; set; } = true;
    public bool IsMandatory { get; set; }
    public int DisplayOrder { get; set; }
    public int OptedOutUsers { get; set; }
}

public class NotificationConfigList
{
    public List<NotificationModuleModel> Modules { get; set; } = new();
}

public class SaveNotificationModule
{
    public Guid MenuModuleId { get; set; }
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

public class SaveNotificationConfigRequest
{
    public List<SaveNotificationModule> Items { get; set; } = new();
}

// ---------- User ----------

public class MyNotificationItem
{
    public Guid ConfigId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Icon { get; set; }
    public string ModuleName { get; set; } = string.Empty;

    /// <summary>The type's code. A built-in type's name is the "push.{Kind}.name" label.</summary>
    public string Kind { get; set; } = "reminder";

    /// <summary>
    /// "Device": a plain reminder this phone schedules itself. "UserTime": the seeker picks one
    /// time and the server sends it. Anything else is sent by the server on its own schedule.
    /// </summary>
    public string Schedule { get; set; } = "Device";

    /// <summary>True when an administrator typed the title or description; shown as typed, not translated.</summary>
    public bool HasCustomText { get; set; }

    /// <summary>For a "UserTime" type: when it goes out until the seeker picks a time.</summary>
    public TimeOnly? DefaultTime { get; set; }

    public bool IsMandatory { get; set; }
    public bool IsEnabled { get; set; }
    public TimeOnly? FromTime { get; set; }
    public TimeOnly? ToTime { get; set; }
    public bool IsActiveNow { get; set; }
    public string? InactiveReason { get; set; }
}

public class MyNotificationSettings
{
    public bool MasterEnabled { get; set; } = true;
    public bool QuietHoursEnabled { get; set; }
    public TimeOnly? QuietFrom { get; set; }
    public TimeOnly? QuietTo { get; set; }
    public TimeOnly CurrentTime { get; set; }
    public bool InQuietHoursNow { get; set; }

    /// <summary>Whether the server can push at all. False means the phone keeps its own alarms.</summary>
    public bool PushEnabled { get; set; }

    public List<MyNotificationItem> Items { get; set; } = new();
}

public class SaveMyNotificationItem
{
    public Guid ConfigId { get; set; }
    public bool IsEnabled { get; set; }
    public TimeOnly? FromTime { get; set; }
    public TimeOnly? ToTime { get; set; }
}

public class SaveMyNotificationSettingsRequest
{
    public bool MasterEnabled { get; set; } = true;
    public bool QuietHoursEnabled { get; set; }
    public TimeOnly? QuietFrom { get; set; }
    public TimeOnly? QuietTo { get; set; }
    public List<SaveMyNotificationItem> Items { get; set; } = new();
}
