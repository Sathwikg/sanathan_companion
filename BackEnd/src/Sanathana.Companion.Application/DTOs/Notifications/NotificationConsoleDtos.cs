namespace Sanathana.Companion.Application.DTOs.Notifications;

/// <summary>Whether a push can leave the server, and why not when it cannot.</summary>
public class ConsolePushStatusDto
{
    public bool Enabled { get; set; }
    public bool Ready { get; set; }
    public string? ProjectId { get; set; }

    /// <summary>What is missing, in words an administrator can act on. Null when ready.</summary>
    public string? Reason { get; set; }

    public bool DataOnly { get; set; }
    public int GraceMinutes { get; set; }
    public int HistoryDays { get; set; }
}

/// <summary>The recurring job that drives the engine, as Hangfire reports it.</summary>
public class ConsoleEngineStatusDto
{
    /// <summary>False when background jobs are switched off, in which case nothing is ever sent on a schedule.</summary>
    public bool Scheduled { get; set; }

    public string? Cron { get; set; }
    public DateTime? LastRunUtc { get; set; }
    public DateTime? NextRunUtc { get; set; }

    /// <summary>"Succeeded", "Failed", … as Hangfire names the last execution's state.</summary>
    public string? LastState { get; set; }
}

public class ConsoleDayDto
{
    public DateOnly Date { get; set; }
    public int Sent { get; set; }
    public int Failed { get; set; }
}

/// <summary>One notification type as the console's job list shows it.</summary>
public class ConsoleKindDto
{
    public Guid? ConfigId { get; set; }
    public string Kind { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string ModuleName { get; set; } = string.Empty;
    public string? Icon { get; set; }

    /// <summary>"UserTime", "DailyAt", "BeforeWindow" or "Manual".</summary>
    public string Schedule { get; set; } = string.Empty;

    /// <summary>The send time in force (configured, else the built-in), for the once-a-day types.</summary>
    public TimeOnly? SendTime { get; set; }

    /// <summary>The lead time in force, for the window warnings.</summary>
    public int? LeadMinutes { get; set; }

    /// <summary>The streak length in force, for the streak notices.</summary>
    public int? Threshold { get; set; }

    /// <summary>The English wording as it is sent, placeholders unfilled, so the admin can see what goes out.</summary>
    public string? TitleTemplate { get; set; }
    public string? BodyTemplate { get; set; }

    /// <summary>False when the type is switched off, its form is inactive, or its form is missing.</summary>
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

public class ConsoleOverviewDto
{
    public ConsolePushStatusDto Push { get; set; } = new();
    public ConsoleEngineStatusDto Engine { get; set; } = new();

    public int TotalDevices { get; set; }
    public int ActiveDevices { get; set; }
    public int AndroidDevices { get; set; }
    public int IosDevices { get; set; }

    /// <summary>Seekers with at least one active device: the most a broadcast can reach.</summary>
    public int ReachableSeekers { get; set; }

    public int SentToday { get; set; }
    public int FailedToday { get; set; }
    public DateTime? LastSentUtc { get; set; }

    /// <summary>The last fourteen India days, oldest first, with zeroes for quiet days.</summary>
    public List<ConsoleDayDto> Days { get; set; } = new();

    public List<ConsoleKindDto> Kinds { get; set; } = new();
}

public class ConsoleLogDto
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

public class ConsoleLogPageDto
{
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public List<ConsoleLogDto> Items { get; set; } = new();
}

public class ConsoleRunDto
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

public class ConsoleDeviceDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string? UserName { get; set; }

    /// <summary>1 Android, 2 iOS.</summary>
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

    /// <summary>The last few characters only: enough to tell two devices apart, not enough to send to one.</summary>
    public string TokenTail { get; set; } = string.Empty;
}

/// <summary>What a "run now" or a test did.</summary>
public class ConsoleActionResultDto
{
    public bool Ok { get; set; }

    /// <summary>One sentence for the toast.</summary>
    public string Message { get; set; } = string.Empty;

    public int Due { get; set; }
    public int Sent { get; set; }
    public int Failed { get; set; }
}

public class ConsoleTestRequestDto
{
    /// <summary>Who receives the sample. Empty means the administrator pressing the button.</summary>
    public Guid? UserId { get; set; }
}

public class ConsoleEnabledRequestDto
{
    public bool Enabled { get; set; }
}

public class ConsoleBroadcastRequestDto
{
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;

    /// <summary>The in-app page a tap opens ("/calendar"). Empty opens the home.</summary>
    public string? Route { get; set; }

    /// <summary>Optional https picture.</summary>
    public string? ImageUrl { get; set; }
}
