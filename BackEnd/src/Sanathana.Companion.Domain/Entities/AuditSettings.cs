using Sanathana.Companion.Domain.Common;

namespace Sanathana.Companion.Domain.Entities;

/// <summary>
/// App-wide audit and error logging settings. Exactly one row.
/// </summary>
public class AuditSettings : BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Master switch. If false, all auditing is disabled.</summary>
    public bool IsGlobalAuditEnabled { get; set; } = true;

    /// <summary>Track login, logout, and session duration.</summary>
    public bool TrackUserSessions { get; set; } = true;

    /// <summary>Track page visits, navigation, and time spent on forms.</summary>
    public bool TrackPageNavigation { get; set; } = true;

    /// <summary>Track database entity insertions, updates, and deletions.</summary>
    public bool TrackDataModifications { get; set; } = true;

    /// <summary>Capture backend unhandled exceptions and frontend client crashes.</summary>
    public bool TrackErrorLogs { get; set; } = true;

    /// <summary>Retention duration in days for audit logs before cleanup.</summary>
    public int AuditRetentionDays { get; set; } = 90;

    /// <summary>Retention duration in days for error logs before cleanup.</summary>
    public int ErrorRetentionDays { get; set; } = 30;
}
