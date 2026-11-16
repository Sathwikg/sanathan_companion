namespace Sanathana.Companion.Application.DTOs.Users;

/// <summary>A seeker's consents and nominee, for the Privacy &amp; Your Data screen and the notice prompt.</summary>
public class PrivacySettingsDto
{
    /// <summary>The notice version this seeker last accepted; null for an account from before versioning.</summary>
    public int? AcceptedNoticeVersion { get; set; }

    public int CurrentNoticeVersion { get; set; }
    public DateOnly CurrentNoticeEffectiveDate { get; set; }

    /// <summary>True when the current notice has not been accepted: the app shows it before anything else.</summary>
    public bool NoticeUpdateRequired { get; set; }

    public bool AdultConfirmed { get; set; }
    public DateTime? NoticeAcceptedAtUtc { get; set; }

    public bool UsageAnalytics { get; set; }

    public string? NomineeName { get; set; }
    public string? NomineeRelationship { get; set; }
    public string? NomineeContact { get; set; }

    public List<ConsentHistoryItemDto> History { get; set; } = new();
}

public class ConsentHistoryItemDto
{
    public string Purpose { get; set; } = string.Empty;
    public bool Granted { get; set; }
    public int NoticeVersion { get; set; }
    public DateTime AtUtc { get; set; }
    public string? Platform { get; set; }
}

public class AcceptNoticeDto
{
    public int NoticeVersion { get; set; }
    public bool ConfirmAdult { get; set; }
}

public class SetConsentDto
{
    public bool Enabled { get; set; }
}

public class NomineeDto
{
    public string? Name { get; set; }
    public string? Relationship { get; set; }
    public string? Contact { get; set; }
}
