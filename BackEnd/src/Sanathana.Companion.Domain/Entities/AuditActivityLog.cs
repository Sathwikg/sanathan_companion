namespace Sanathana.Companion.Domain.Entities;

/// <summary>
/// Records when a user navigates to a form/module and the exact time spent in it.
/// </summary>
public class AuditActivityLog
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The sign-in this visit happened in; see <see cref="AuditUserSession"/>.</summary>
    public Guid? SessionId { get; set; }
    public Guid? UserId { get; set; }
    public string? UsernameOrEmail { get; set; }

    public string ModuleCode { get; set; } = string.Empty;
    public string? FormName { get; set; }
    public string RoutePath { get; set; } = string.Empty;

    public DateTime EnteredAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ExitedAtUtc { get; set; }
    public int TimeSpentSeconds { get; set; }

    public string? Platform { get; set; }
    public string? IpAddress { get; set; }
}
