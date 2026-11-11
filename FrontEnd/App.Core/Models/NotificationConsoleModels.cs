namespace App.Core.Models;

// The administrator's notification console (GET/POST notification-console/*), and the seeker's own
// inbox and device registration (notifications/inbox, notifications/devices).

public sealed class ConsolePushStatus
{
    public bool Enabled { get; set; }
    public bool Ready { get; set; }
    public string? ProjectId { get; set; }
    public string? Reason { get; set; }
    public bool DataOnly { get; set; }
    public int GraceMinutes { get; set; }
    public int HistoryDays { get; set; }
}

public sealed class ConsoleEngineStatus
{
    public bool Scheduled { get; set; }
    public string? Cron { get; set; }
    public DateTime? LastRunUtc { get; set; }
    public DateTime? NextRunUtc { get; set; }
    public string? LastState { get; set; }
}

public sealed class ConsoleDay
{
    public DateOnly Date { get; set; }
    public int Sent { get; set; }
    public int Failed { get; set; }
}

public sealed class ConsoleKind
{
    public Guid? ConfigId { get; set; }
    public string Kind { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string ModuleName { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public string Schedule { get; set; } = string.Empty;
    public TimeOnly? SendTime { get; set; }
    public int? LeadMinutes { get; set; }
    public int? Threshold { get; set; }
    public string? TitleTemplate { get; set; }
    public string? BodyTemplate { get; set; }
    public bool IsEnabled { get; set; }
    public bool DefaultOn { get; set; }
    public bool IsMandatory { get; set; }
    public int SentToday { get; set; }
    public int FailedToday { get; set; }
    public int SentLast7Days { get; set; }
    public DateTime? LastRunUtc { get; set; }
    public int? LastDue { get; set; }
    public int? LastSent { get; set; }
    public int? LastFailed { get; set; }
    public string? LastTrigger { get; set; }
    public string? LastError { get; set; }
}

public sealed class ConsoleOverview
{
    public ConsolePushStatus Push { get; set; } = new();
    public ConsoleEngineStatus Engine { get; set; } = new();
    public int TotalDevices { get; set; }
    public int ActiveDevices { get; set; }
    public int AndroidDevices { get; set; }
    public int IosDevices { get; set; }
    public int ReachableSeekers { get; set; }
    public int SentToday { get; set; }
    public int FailedToday { get; set; }
    public DateTime? LastSentUtc { get; set; }
    public List<ConsoleDay> Days { get; set; } = new();
    public List<ConsoleKind> Kinds { get; set; } = new();
}

public sealed class ConsoleLog
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string? UserName { get; set; }
    public string Kind { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int Devices { get; set; }
    public string? Error { get; set; }
    public string Trigger { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; }
    public DateTime? ReadAtUtc { get; set; }
}

public sealed class ConsoleLogPage
{
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public List<ConsoleLog> Items { get; set; } = new();
}

public sealed class ConsoleRun
{
    public Guid Id { get; set; }
    public string Kind { get; set; } = string.Empty;
    public DateTime StartedAtUtc { get; set; }
    public int DurationMs { get; set; }
    public int Due { get; set; }
    public int Sent { get; set; }
    public int Failed { get; set; }
    public string Trigger { get; set; } = string.Empty;
    public string? Error { get; set; }
}

public sealed class ConsoleDevice
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string? UserName { get; set; }
    public int Platform { get; set; }
    public string? DeviceModel { get; set; }
    public string? AppVersion { get; set; }
    public string? Language { get; set; }
    public string? TimeZone { get; set; }
    public bool IsActive { get; set; }
    public int FailureCount { get; set; }
    public string? LastFailureReason { get; set; }
    public DateTime RegisteredUtc { get; set; }
    public DateTime? LastUsedUtc { get; set; }
    public string TokenTail { get; set; } = string.Empty;
}

public sealed class ConsoleActionResult
{
    public bool Ok { get; set; }
    public string Message { get; set; } = string.Empty;
    public int Due { get; set; }
    public int Sent { get; set; }
    public int Failed { get; set; }
}

public sealed class ConsoleBroadcastRequest
{
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string? Route { get; set; }
    public string? ImageUrl { get; set; }
}

// ---------- the seeker's phone and inbox ----------

/// <summary>What a phone tells the server so notifications can reach it (POST notifications/devices).</summary>
public sealed class RegisterDeviceRequest
{
    /// <summary>1 Android, 2 iOS.</summary>
    public int Platform { get; set; }
    public string Token { get; set; } = string.Empty;
    public string? DeviceModel { get; set; }
    public string? AppVersion { get; set; }
    public string? TimeZone { get; set; }
    public string? Language { get; set; }
}

public sealed class InboxItem
{
    public Guid Id { get; set; }
    public string Kind { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string? Route { get; set; }
    public DateTime SentAtUtc { get; set; }
    public bool IsRead { get; set; }
}

public sealed class Inbox
{
    public int Unread { get; set; }
    public List<InboxItem> Items { get; set; } = new();
}
