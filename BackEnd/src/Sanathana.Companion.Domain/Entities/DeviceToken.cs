using Sanathana.Companion.Domain.Common;

namespace Sanathana.Companion.Domain.Entities;

/// <summary>
/// Device token for push notifications (FCM/APNs).
/// </summary>
public class DeviceToken : BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    /// <summary>Platform: Android (FCM), iOS (APNs), Web (Web Push).</summary>
    public int Platform { get; set; }

    /// <summary>The push token (FCM registration token or APNs device token).</summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>Optional device model for debugging.</summary>
    public string? DeviceModel { get; set; }

    /// <summary>Optional app version for debugging.</summary>
    public string? AppVersion { get; set; }

    /// <summary>Whether this token is active and can receive notifications.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Last time this token was successfully used.</summary>
    public DateTime? LastUsedAt { get; set; }

    /// <summary>Number of consecutive failures (for retry logic).</summary>
    public int FailureCount { get; set; } = 0;

    /// <summary>Last failure reason (for debugging).</summary>
    public string? LastFailureReason { get; set; }
}