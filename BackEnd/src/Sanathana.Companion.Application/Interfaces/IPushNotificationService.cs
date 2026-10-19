using Sanathana.Companion.Application.DTOs.Notifications;

namespace Sanathana.Companion.Application.Interfaces;

/// <summary>
/// Service for managing and sending push notifications via FCM (Android) and APNs (iOS).
/// </summary>
public interface IPushNotificationService
{
    // ========== Device Token Management ==========

    /// <summary>
    /// Registers or updates a device token for a user.
    /// </summary>
    Task<DeviceTokenDto> RegisterDeviceTokenAsync(Guid userId, RegisterDeviceTokenRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all active device tokens for a user.
    /// </summary>
    Task<IReadOnlyList<DeviceTokenDto>> GetUserDeviceTokensAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a device token (e.g., on logout or token invalidation).
    /// </summary>
    Task<bool> RemoveDeviceTokenAsync(Guid userId, string token, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes all device tokens for a user.
    /// </summary>
    Task RemoveAllUserDeviceTokensAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a device token as inactive (e.g., when FCM/APNs returns invalid token).
    /// </summary>
    Task MarkTokenInactiveAsync(string token, string reason, CancellationToken cancellationToken = default);

    // ========== Sending Notifications ==========

    /// <summary>
    /// Sends a push notification to specific users.
    /// </summary>
    Task<PushNotificationResult> SendToUsersAsync(SendPushNotificationRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a push notification to all users subscribed to a module's notifications.
    /// </summary>
    Task<PushNotificationResult> SendToModuleSubscribersAsync(SendModuleNotificationRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a test notification to a specific user.
    /// </summary>
    Task<PushNotificationResult> SendTestNotificationAsync(Guid userId, string title, string body, CancellationToken cancellationToken = default);

    // ========== Statistics ==========

    /// <summary>
    /// Gets push notification statistics.
    /// </summary>
    Task<PushNotificationStatsDto> GetStatsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Cleans up inactive/expired device tokens.
    /// </summary>
    Task<int> CleanupInactiveTokensAsync(int olderThanDays = 30, CancellationToken cancellationToken = default);
}