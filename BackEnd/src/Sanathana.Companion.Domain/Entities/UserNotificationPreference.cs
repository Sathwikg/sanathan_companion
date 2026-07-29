using Sanathana.Companion.Domain.Common;

namespace Sanathana.Companion.Domain.Entities;

/// <summary>
/// A user's choice for one notification type. Rows exist only where the user has expressed a
/// preference — everyone else follows <see cref="NotificationConfig.DefaultEnabledForUsers"/>.
/// That keeps the table small and lets new notification types reach existing users automatically.
/// </summary>
public class UserNotificationPreference : BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }

    public Guid NotificationConfigId { get; set; }
    public NotificationConfig? NotificationConfig { get; set; }

    public bool IsEnabled { get; set; } = true;

    /// <summary>Optional preferred delivery window for this type (may wrap past midnight).</summary>
    public TimeOnly? FromTime { get; set; }
    public TimeOnly? ToTime { get; set; }
}
