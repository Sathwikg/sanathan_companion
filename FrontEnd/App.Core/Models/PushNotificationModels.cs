namespace App.Core.Models;

/// <summary>
/// Push notification platform types.
/// </summary>
public enum PushPlatform
{
    Unknown = 0,
    Android = 1,
    iOS = 2,
    Web = 3
}

/// <summary>
/// Device token DTO.
/// </summary>
public sealed record DeviceTokenDto(
    Guid Id,
    Guid UserId,
    PushPlatform Platform,
    string Token,
    string? DeviceModel,
    string? AppVersion,
    DateTime CreatedAt,
    DateTime? LastUsedAt,
    bool IsActive
);

/// <summary>
/// Request to register a device token.
/// </summary>
public sealed record RegisterDeviceTokenRequest(
    PushPlatform Platform,
    string Token,
    string? DeviceModel = null,
    string? AppVersion = null
);

/// <summary>
/// Request to send a push notification to specific users.
/// </summary>
public sealed record SendPushNotificationRequest(
    IEnumerable<Guid> UserIds,
    string Title,
    string Body,
    Dictionary<string, string>? Data = null,
    string? ImageUrl = null,
    int? Badge = null,
    string? Sound = null,
    int TimeToLiveSeconds = 86400,
    PushPlatform? TargetPlatform = null
);

/// <summary>
/// Request to send a push notification to module subscribers.
/// </summary>
public sealed record SendModuleNotificationRequest(
    Guid MenuModuleId,
    string Title,
    string Body,
    Dictionary<string, string>? Data = null,
    string? ImageUrl = null,
    PushPlatform? TargetPlatform = null
);

/// <summary>
/// Request to send a test notification.
/// </summary>
public sealed record SendTestNotificationRequest(
    Guid UserId,
    string Title,
    string Body
);

/// <summary>
/// Result of a push notification send operation.
/// </summary>
public sealed record PushNotificationResult(
    bool Success,
    int SentCount,
    int FailedCount,
    List<string> Errors,
    Dictionary<Guid, string>? FailedTokens = null
);

/// <summary>
/// Push notification statistics.
/// </summary>
public sealed record PushNotificationStatsDto(
    int TotalDevices,
    int ActiveDevices,
    int AndroidDevices,
    int iOSDevices,
    int NotificationsSentToday,
    int NotificationsFailedToday,
    DateTime LastSentAt
);