using Sanathana.Companion.Domain.Common;

namespace Sanathana.Companion.Domain.Entities;

/// <summary>
/// One pass of the notification engine over one notification type, kept only when the pass had
/// something to do (or failed). The administrator's console lists these as the type's run history.
/// </summary>
/// <remarks>
/// The engine wakes every minute and most minutes nothing is due; recording those would bury the
/// runs that matter under fourteen hundred empty rows a day per type. The heartbeat that proves the
/// engine is alive is the recurring job's own "last execution", which Hangfire already keeps.
/// </remarks>
public class NotificationRun : BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Kind { get; set; } = string.Empty;

    public DateTime StartedAtUtc { get; set; }
    public int DurationMs { get; set; }

    /// <summary>Seekers the type was due for on this pass.</summary>
    public int Due { get; set; }
    public int Sent { get; set; }
    public int Failed { get; set; }

    /// <summary>One of <see cref="NotificationTriggers"/>.</summary>
    public string Trigger { get; set; } = NotificationTriggers.Schedule;

    /// <summary>The exception that stopped the pass, if one did.</summary>
    public string? Error { get; set; }
}
