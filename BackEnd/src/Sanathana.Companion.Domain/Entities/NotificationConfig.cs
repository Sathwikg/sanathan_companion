using Sanathana.Companion.Domain.Common;

namespace Sanathana.Companion.Domain.Entities;

/// <summary>
/// Admin configuration: makes one menu module notification-capable. A module without a row here
/// simply cannot raise notifications.
/// </summary>
public class NotificationConfig : BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The form/module this notification belongs to.</summary>
    public Guid MenuModuleId { get; set; }
    public MenuModule? MenuModule { get; set; }

    /// <summary>Headline shown to users; falls back to the module name when blank.</summary>
    public string? Title { get; set; }

    /// <summary>Explains to the user what this notification is for.</summary>
    public string? Description { get; set; }

    /// <summary>Whether users receive it unless they opt out (users with no saved preference follow this).</summary>
    public bool DefaultEnabledForUsers { get; set; } = true;

    /// <summary>Users cannot turn this one off (e.g. important announcements).</summary>
    public bool IsMandatory { get; set; }

    public int DisplayOrder { get; set; }

    /// <summary>Admin master switch for this notification type.</summary>
    public bool IsActive { get; set; } = true;
}
