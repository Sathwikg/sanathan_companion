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
    /// FCM Service Account JSON (path or JSON content). Required for Android.
    /// </summary>
    public string? FcmServiceAccountPath { get; set; }

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
    public string DefaultTitle { get; set; } = "Sanathana Companion";

    /// <summary>
    /// Default notification icon (Android).
    /// </summary>
    public string? DefaultIcon { get; set; }

    /// <summary>
    /// Default notification color (Android, hex).
    /// </summary>
    public string? DefaultColor { get; set; } = "#FF9933";

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

    public bool IsFcmConfigured => !string.IsNullOrWhiteSpace(FcmServiceAccountPath) || !string.IsNullOrWhiteSpace(FcmProjectId);

    public bool IsApnsConfigured => !string.IsNullOrWhiteSpace(ApnsKeyId) && !string.IsNullOrWhiteSpace(ApnsTeamId) && !string.IsNullOrWhiteSpace(ApnsPrivateKeyPath);
}