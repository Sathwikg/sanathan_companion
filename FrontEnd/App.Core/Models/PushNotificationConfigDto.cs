namespace App.Core.Models;

/// <summary>
/// Push notification configuration DTO.
/// </summary>
public sealed class PushNotificationConfigDto
{
    public bool Enabled { get; set; }
    public string? FcmServiceAccountPath { get; set; }
    public string? FcmProjectId { get; set; }
    public string? FcmSenderId { get; set; }
    public string? ApnsKeyId { get; set; }
    public string? ApnsTeamId { get; set; }
    public string? ApnsBundleId { get; set; }
    public string? ApnsPrivateKeyPath { get; set; }
    public bool ApnsUseSandbox { get; set; } = true;
    public string? DefaultTitle { get; set; }
    public string? DefaultIcon { get; set; }
    public string? DefaultColor { get; set; }
    public int TimeToLiveSeconds { get; set; } = 86400;
    public int MaxRetryAttempts { get; set; } = 3;
    public int BatchSize { get; set; } = 500;
}