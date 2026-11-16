namespace Sanathana.Companion.Application.Common;

/// <summary>
/// The privacy notice a seeker consents to (Digital Personal Data Protection Act, 2023, sections 5
/// and 6; DPDP Rules, 2025, rule 3).
/// </summary>
/// <remarks>
/// Raise <see cref="CurrentVersion"/> whenever the notice changes what is collected, why, or who it
/// is shared with. Every signed-in seeker whose accepted version is lower is then shown the new
/// notice once and asked to confirm it, and the confirmation is recorded (section 6(10) puts the
/// burden of proving consent on the app). Wording-only edits need no new version.
/// </remarks>
public static class PrivacyNotice
{
    public const int CurrentVersion = 2;

    /// <summary>The date the current version took effect, shown on the notice.</summary>
    public static readonly DateOnly EffectiveDate = new(2026, 11, 15);
}

/// <summary>What a consent record is about. Stored as-is.</summary>
public static class ConsentPurposes
{
    /// <summary>The privacy notice itself: the processing the service cannot run without.</summary>
    public const string PrivacyNotice = "privacyNotice";

    /// <summary>
    /// Recording which screens a seeker opens and for how long. Not needed to provide the service,
    /// and behavioural monitoring under section 9(3) when the user is a child, so it is a separate,
    /// optional consent that is OFF until the seeker turns it on.
    /// </summary>
    public const string UsageAnalytics = "usageAnalytics";

    public static readonly IReadOnlyList<string> All = new[] { PrivacyNotice, UsageAnalytics };
}

/// <summary>How long security and processing logs must be kept under Indian law.</summary>
public static class LegalRetention
{
    /// <summary>
    /// One year. CERT-In's directions of 28 April 2022 (section 70B(6), IT Act) require logs of all
    /// ICT systems for a rolling 180 days, already in force; DPDP Rules 2025 rule 6(1)(e) and
    /// rule 8(3) require one year from 13 May 2027. The longer of the two is the floor every log
    /// retention setting in the app is held to.
    /// </summary>
    public const int MinimumLogDays = 365;
}
