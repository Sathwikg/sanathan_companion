namespace Sanathana.Companion.Application.DTOs.Audit;

public class AuditSettingsDto
{
    public bool IsGlobalAuditEnabled { get; set; } = true;
    public bool TrackUserSessions { get; set; } = true;
    public bool TrackPageNavigation { get; set; } = true;
    public bool TrackDataModifications { get; set; } = true;
    public bool TrackErrorLogs { get; set; } = true;
    public int AuditRetentionDays { get; set; } = 90;
    public int ErrorRetentionDays { get; set; } = 30;
}

public class AuditModuleConfigDto
{
    public Guid MenuModuleId { get; set; }
    public string ModuleName { get; set; } = string.Empty;
    public string ModuleCode { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public string? ParentName { get; set; }
    public bool IsActivityAuditEnabled { get; set; } = true;
    public bool IsDataAuditEnabled { get; set; } = true;
}

public class AuditConfigResponseDto
{
    public AuditSettingsDto Settings { get; set; } = new();
    public List<AuditModuleConfigDto> Modules { get; set; } = new();
}

public class SaveAuditModuleDto
{
    public Guid MenuModuleId { get; set; }
    public bool IsActivityAuditEnabled { get; set; }
    public bool IsDataAuditEnabled { get; set; }
}

public class SaveAuditConfigDto
{
    public AuditSettingsDto Settings { get; set; } = new();
    public List<SaveAuditModuleDto> Modules { get; set; } = new();
}

/// <summary>
/// One page visit, sent when the user leaves the page. The server decides which form the route
/// belongs to and when the visit happened: only the duration is taken from the client, so a
/// device with a wrong clock cannot misdate the log.
/// </summary>
public class LogActivityRequestDto
{
    public string RoutePath { get; set; } = string.Empty;
    public int TimeSpentSeconds { get; set; }
}

public class LogErrorRequestDto
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

/// <summary>Who sent a telemetry record, as the API layer saw the request.</summary>
public sealed record AuditCaller(
    Guid? UserId,
    string? Email,
    Guid? SessionId,
    string? IpAddress,
    string? UserAgent,
    string? Platform);

/// <summary>Paging and filters shared by the four log lists. Dates are UTC calendar days.</summary>
public class AuditLogQueryDto
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
    public string? Search { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }

    /// <summary>Data changes only: INSERT, UPDATE or DELETE.</summary>
    public string? Action { get; set; }

    /// <summary>Errors only: BackendApi, FrontendWeb or FrontendMobile.</summary>
    public string? Source { get; set; }

    /// <summary>Errors only: true for resolved, false for open.</summary>
    public bool? Resolved { get; set; }
}

public class PagedResultDto<T>
{
    public List<T> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

public class AuditSessionLogDto
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

public class AuditActivityLogDto
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

public class AuditDataLogDto
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

public class ErrorLogDto
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
