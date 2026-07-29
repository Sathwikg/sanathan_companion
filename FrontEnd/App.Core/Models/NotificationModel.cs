namespace App.Core.Models;

// ---------- Admin ----------

public class NotificationModuleModel
{
    public Guid MenuModuleId { get; set; }
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
