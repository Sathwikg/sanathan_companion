using System.ComponentModel.DataAnnotations;

namespace Sanathana.Companion.Application.Common;

/// <summary>
/// Configuration for push notifications (FCM/APNs).
/// </summary>
public sealed class PushNotificationOptions
{
    public const string SectionName = "PushNotifications";

    /// <summary>
    /// Enables push notifications. Default: false.
    /// </summary>
    public bool Enabled { get; set; } = false;

    // ========== FCM (Android) ==========

    /// <summary>
    /// Path to the Firebase service account key file (the JSON downloaded from Firebase console,
    /// Project settings, Service accounts). Use this on a developer machine.
    /// </summary>
    public string? FcmServiceAccountPath { get; set; }

    /// <summary>
    /// The same key file's CONTENT, for hosts where a secret is an environment variable rather than
    /// a file (PushNotifications__FcmServiceAccountJson). Raw JSON or the JSON base64-encoded, which
    /// survives .env files and shells that mangle quotes and newlines. Wins over the path when both are set.
    /// </summary>
    public string? FcmServiceAccountJson { get; set; }

    /// <summary>
    /// FCM Project ID (extracted from service account if not provided).
    /// </summary>
    public string? FcmProjectId { get; set; }

    /// <summary>
    /// FCM Sender ID.
    /// </summary>
    public string? FcmSenderId { get; set; }

    // ========== APNs (iOS/macOS) ==========

    /// <summary>
    /// APNs Key ID (from Apple Developer Portal).
    /// </summary>
    public string? ApnsKeyId { get; set; }

    /// <summary>
    /// APNs Team ID (from Apple Developer Portal).
    /// </summary>
    public string? ApnsTeamId { get; set; }

    /// <summary>
    /// APNs Bundle ID (your app's bundle identifier, e.g., com.sanathana.companion).
    /// </summary>
    public string? ApnsBundleId { get; set; }

    /// <summary>
    /// APNs Private Key (P8 file path or PEM content).
    /// </summary>
    public string? ApnsPrivateKeyPath { get; set; }

    /// <summary>
    /// Use APNs sandbox environment (development). Default: true.
    /// </summary>
    public bool ApnsUseSandbox { get; set; } = true;

    /// <summary>
    /// APNs Topic (usually the bundle ID).
    /// </summary>
    public string? ApnsTopic { get; set; }

    // ========== General ==========

    /// <summary>
    /// Default notification title if not provided.
    /// </summary>
    public string DefaultTitle { get; set; } = "Nitya Sannidhi";

    /// <summary>
    /// Default notification icon (Android).
    /// </summary>
    public string? DefaultIcon { get; set; }

    /// <summary>
    /// Default notification color (Android, hex).
    /// </summary>
    public string? DefaultColor { get; set; } = "#D47222";

    /// <summary>
    /// Time-to-live for notifications in seconds. Default: 24 hours.
    /// </summary>
    public int TimeToLiveSeconds { get; set; } = 86400;

    /// <summary>
    /// Maximum retry attempts for failed deliveries. Default: 3.
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Batch size for sending notifications. Default: 500.
    /// </summary>
    public int BatchSize { get; set; } = 500;

    // ========== Engine ==========

    /// <summary>
    /// How long after its moment a scheduled notification is still sent. Covers a late run or a
    /// restart; past it the moment is let go. Default: 30 minutes.
    /// </summary>
    public int GraceMinutes { get; set; } = 30;

    /// <summary>How long the delivery history (and with it each seeker's inbox) is kept. Default: 60 days.</summary>
    public int HistoryDays { get; set; } = 60;

    /// <summary>
    /// Send Android messages as data only, leaving the app to draw every notification itself. Off by
    /// default: a message with a notification part is shown by Google Play services even when the
    /// phone's battery manager will not start the app, which is the common case on the Android
    /// skins most seekers use. Turn on only for full control of the artwork at the cost of that.
    /// </summary>
    public bool DataOnly { get; set; }

    public bool IsFcmConfigured => !string.IsNullOrWhiteSpace(FcmServiceAccountJson) || !string.IsNullOrWhiteSpace(FcmServiceAccountPath) || !string.IsNullOrWhiteSpace(FcmProjectId);

    public bool IsApnsConfigured => !string.IsNullOrWhiteSpace(ApnsKeyId) && !string.IsNullOrWhiteSpace(ApnsTeamId) && !string.IsNullOrWhiteSpace(ApnsPrivateKeyPath);
}