namespace Sanathana.Companion.Domain.Entities;

/// <summary>
/// Records server-side and client-side unhandled errors and exceptions for debugging and triage.
/// </summary>
public class ErrorLog
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;

    /// <summary>BackendApi, FrontendWeb, FrontendMobile</summary>
    public string Source { get; set; } = "BackendApi";

    /// <summary>Critical, Error, Warning</summary>
    public string Severity { get; set; } = "Error";

    public int? StatusCode { get; set; }
    public string ExceptionType { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? StackTrace { get; set; }
    public string? InnerException { get; set; }

    public string? RequestPath { get; set; }
    public string? RequestMethod { get; set; }

    public Guid? UserId { get; set; }
    public string? UsernameOrEmail { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }

    public bool IsResolved { get; set; } = false;
    public DateTime? ResolvedAtUtc { get; set; }
    public string? ResolvedBy { get; set; }
}
