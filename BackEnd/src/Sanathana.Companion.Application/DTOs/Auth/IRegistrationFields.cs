namespace Sanathana.Companion.Application.DTOs.Auth;

/// <summary>
/// The fields every way of creating an account collects, so the plain and the Google registration
/// validators share one set of rules instead of drifting apart.
/// </summary>
public interface IRegistrationFields
{
    string FullName { get; }
    string MobileNumber { get; }
    string Password { get; }
    string ConfirmPassword { get; }
    string? SeekerName { get; }

    /// <summary>Consent to the privacy notice: unticked until the seeker ticks it (DPDP Act 2023, section 6).</summary>
    bool AcceptPrivacyNotice { get; }

    /// <summary>Accounts are for adults; a child's data would need verifiable parental consent (section 9).</summary>
    bool ConfirmAdult { get; }

    int PrivacyNoticeVersion { get; }
}
