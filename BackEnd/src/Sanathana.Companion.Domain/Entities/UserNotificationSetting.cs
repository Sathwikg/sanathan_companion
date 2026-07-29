using Sanathana.Companion.Domain.Common;

namespace Sanathana.Companion.Domain.Entities;

/// <summary>One row per user holding their global notification settings.</summary>
public class UserNotificationSetting : BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }

    /// <summary>Pause everything. Mandatory notifications still get through.</summary>
    public bool MasterEnabled { get; set; } = true;

    /// <summary>Suppress notifications during a daily window (may wrap past midnight).</summary>
    public bool QuietHoursEnabled { get; set; }
    public TimeOnly? QuietFrom { get; set; }
    public TimeOnly? QuietTo { get; set; }
}
