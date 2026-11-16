namespace Sanathana.Companion.Application.DTOs.Auth;

public class RegisterRequestDto : IRegistrationFields
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string MobileNumber { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
    public string? SeekerName { get; set; }

    /// <summary>Optional preferred region — becomes the seeker's default region.</summary>
    public Guid? RegionId { get; set; }

    /// <summary>The seeker ticked "I have read and agree to the Privacy Notice" (unticked by default).</summary>
    public bool AcceptPrivacyNotice { get; set; }

    /// <summary>The seeker ticked "I am 18 years of age or older".</summary>
    public bool ConfirmAdult { get; set; }

    /// <summary>The notice version that was on screen, so an outdated app cannot consent to a notice it never showed.</summary>
    public int PrivacyNoticeVersion { get; set; }
}
