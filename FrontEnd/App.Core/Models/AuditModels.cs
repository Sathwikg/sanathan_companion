namespace App.Core.Models;

public class AuditSettingsModel
{
    public bool IsGlobalAuditEnabled { get; set; } = true;
    public bool TrackUserSessions { get; set; } = true;
    public bool TrackPageNavigation { get; set; } = true;
    public bool TrackDataModifications { get; set; } = true;
    public bool TrackErrorLogs { get; set; } = true;
    public int AuditRetentionDays { get; set; } = 90;
    public int ErrorRetentionDays { get; set; } = 30;
}

public class AuditModuleConfigModel
{
    public Guid MenuModuleId { get; set; }
    public string ModuleName { get; set; } = string.Empty;
    public string ModuleCode { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public string? ParentName { get; set; }
    public bool IsActivityAuditEnabled { get; set; } = true;
    public bool IsDataAuditEnabled { get; set; } = true;
}

public class AuditConfigResponseModel
{
    public AuditSettingsModel Settings { get; set; } = new();
    public List<AuditModuleConfigModel> Modules { get; set; } = new();
}

public class SaveAuditModuleModel
{
    public Guid MenuModuleId { get; set; }
    public bool IsActivityAuditEnabled { get; set; }
    public bool IsDataAuditEnabled { get; set; }
}

public class SaveAuditConfigModel
{
    public AuditSettingsModel Settings { get; set; } = new();
    public List<SaveAuditModuleModel> Modules { get; set; } = new();
}

/// <summary>One page visit. The server resolves the form and the timestamps; see NavigationTrackerService.</summary>
public class LogActivityRequestModel
{
    public string RoutePath { get; set; } = string.Empty;
    public int TimeSpentSeconds { get; set; }
}

public class LogErrorRequestModel
{
    public string Source { get; set; } = "FrontendWeb";
    public string Severity { get; set; } = "Error";
    public int? StatusCode { get; set; }
    public string ExceptionType { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? StackTrace { get; set; }
    public string? InnerException { get; set; }
    public string? RequestPath { get; set; }
}

public class AuditSessionLogModel
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public string? UsernameOrEmail { get; set; }
    public DateTime LoginTimeUtc { get; set; }
    public DateTime? LogoutTimeUtc { get; set; }
    public DateTime? LastHeartbeatUtc { get; set; }
    public int? DurationSeconds { get; set; }
    public string? ExitReason { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? Platform { get; set; }
}

public class AuditActivityLogModel
{
    public Guid Id { get; set; }
    public Guid? SessionId { get; set; }
    public Guid? UserId { get; set; }
    public string? UsernameOrEmail { get; set; }
    public string ModuleCode { get; set; } = string.Empty;
    public string? FormName { get; set; }
    public string RoutePath { get; set; } = string.Empty;
    public DateTime EnteredAtUtc { get; set; }
    public DateTime? ExitedAtUtc { get; set; }
    public int TimeSpentSeconds { get; set; }
    public string? Platform { get; set; }
    public string? IpAddress { get; set; }
}

public class AuditDataLogModel
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public string? UsernameOrEmail { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityName { get; set; } = string.Empty;
    public string? EntityId { get; set; }
    public string? ModuleCode { get; set; }
    public string? ChangedColumns { get; set; }
    public string? OldValuesJson { get; set; }
    public string? NewValuesJson { get; set; }
    public DateTime TimestampUtc { get; set; }
    public string? IpAddress { get; set; }
    public string? Endpoint { get; set; }
}

public class ErrorLogModel
{
    public Guid Id { get; set; }
    public DateTime TimestampUtc { get; set; }
    public string Source { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public int? StatusCode { get; set; }
    public string ExceptionType { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? StackTrace { get; set; }
    public string? InnerException { get; set; }
    public string? RequestPath { get; set; }
    public string? RequestMethod { get; set; }
    public string? UsernameOrEmail { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public bool IsResolved { get; set; }
    public DateTime? ResolvedAtUtc { get; set; }
    public string? ResolvedBy { get; set; }
}

/// <summary>Paging and filters for the four log lists. Dates are UTC calendar days.</summary>
public class AuditLogQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
    public string? Search { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public string? Action { get; set; }
    public string? Source { get; set; }
    public bool? Resolved { get; set; }
}

public class AuditPageModel<T>
{
    public List<T> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}
