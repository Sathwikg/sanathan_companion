using Sanathana.Companion.Domain.Entities;

namespace Sanathana.Companion.Domain.Interfaces;

/// <summary>Admin-side notification configuration.</summary>
public interface INotificationConfigRepository : IRepository<NotificationConfig>
{
    /// <summary>All configs (with their module), ordered for display.</summary>
    Task<IReadOnlyList<NotificationConfig>> GetAllWithModuleAsync(CancellationToken cancellationToken = default);

    Task<NotificationConfig?> GetByModuleAsync(Guid menuModuleId, CancellationToken cancellationToken = default);
}

/// <summary>A user's own notification settings and per-type preferences.</summary>
public interface IUserNotificationRepository : IRepository<UserNotificationPreference>
{
    Task<UserNotificationSetting?> GetSettingAsync(Guid userId, CancellationToken cancellationToken = default);

    Task AddSettingAsync(UserNotificationSetting setting, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UserNotificationPreference>> GetPreferencesAsync(Guid userId, CancellationToken cancellationToken = default);
}
