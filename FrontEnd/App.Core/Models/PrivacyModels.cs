namespace App.Core.Models;

/// <summary>
/// The Privacy Notice the app shows and asks consent to. Must match the server's
/// <c>PrivacyNotice.CurrentVersion</c>: the server refuses consent to any other version, so an app
/// that shows an older notice cannot record agreement to a newer one.
/// </summary>
public static class PrivacyNoticeInfo
{
    public const int CurrentVersion = 2;
    public static readonly DateOnly EffectiveDate = new(2026, 11, 15);
    public const string Route = "privacy";

    /// <summary>The seeded "Privacy / My Data Request" issue type (SeedConstants.IssueTypePrivacyId).</summary>
    public static readonly Guid PrivacyIssueTypeId = new("41000000-0000-0000-0000-000000000006");
}

/// <summary>What the seeker agreed to and chose (GET api/profile/privacy).</summary>
public sealed class PrivacySettings
{
    public int? AcceptedNoticeVersion { get; set; }
    public int CurrentNoticeVersion { get; set; }
    public DateOnly CurrentNoticeEffectiveDate { get; set; }
    public bool NoticeUpdateRequired { get; set; }
    public bool AdultConfirmed { get; set; }
    public DateTime? NoticeAcceptedAtUtc { get; set; }
    public bool UsageAnalytics { get; set; }
    public string? NomineeName { get; set; }
    public string? NomineeRelationship { get; set; }
    public string? NomineeContact { get; set; }
    public List<ConsentHistoryItem> History { get; set; } = new();
}

public sealed class ConsentHistoryItem
{
    public string Purpose { get; set; } = string.Empty;
    public bool Granted { get; set; }
    public int NoticeVersion { get; set; }
    public DateTime AtUtc { get; set; }
    public string? Platform { get; set; }
}

public sealed class AcceptNoticeRequest
{
    public int NoticeVersion { get; set; } = PrivacyNoticeInfo.CurrentVersion;
    public bool ConfirmAdult { get; set; }
}

public sealed class NomineeRequest
{
    public string? Name { get; set; }
    public string? Relationship { get; set; }
    public string? Contact { get; set; }
}
