namespace App.Core.Models;

/// <summary>
/// The server's effective push settings (GET push-notifications/config): read-only, and the two credential
/// files are reported only as configured-or-not, never by path or content.
/// </summary>
public sealed class PushNotificationConfigDto
{
    public bool Enabled { get; set; }
    public bool FcmConfigured { get; set; }
    public string? FcmProjectId { get; set; }
    public string? FcmSenderId { get; set; }
    public bool ApnsConfigured { get; set; }
    public string? ApnsKeyId { get; set; }
    public string? ApnsTeamId { get; set; }
    public string? ApnsBundleId { get; set; }
    public bool ApnsUseSandbox { get; set; } = true;
    public string? DefaultTitle { get; set; }
    public string? DefaultIcon { get; set; }
    public string? DefaultColor { get; set; }
    public int TimeToLiveSeconds { get; set; } = 86400;
    public int MaxRetryAttempts { get; set; } = 3;
    public int BatchSize { get; set; } = 500;
}