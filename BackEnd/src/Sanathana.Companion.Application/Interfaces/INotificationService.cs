using Sanathana.Companion.Application.DTOs.Notifications;

namespace Sanathana.Companion.Application.Interfaces;

public interface INotificationService
{
    // ---- Admin ----

    /// <summary>Every navigable form with its notification configuration.</summary>
    Task<NotificationConfigListDto> GetConfigAsync(CancellationToken cancellationToken = default);

    /// <summary>Replaces the notification configuration with the supplied selection.</summary>
    Task SaveConfigAsync(SaveNotificationConfigDto dto, CancellationToken cancellationToken = default);

    // ---- User ----

    /// <summary>The user's notification settings, resolved against what the admin enabled.</summary>
    Task<MyNotificationSettingsDto> GetMySettingsAsync(Guid userId, CancellationToken cancellationToken = default);

    Task SaveMySettingsAsync(Guid userId, SaveMyNotificationSettingsDto dto, CancellationToken cancellationToken = default);
}
