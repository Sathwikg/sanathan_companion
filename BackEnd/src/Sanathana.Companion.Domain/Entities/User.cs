using Sanathana.Companion.Domain.Common;

namespace Sanathana.Companion.Domain.Entities;

/// <summary>Application user / seeker.</summary>
public class User : BaseEntity
{
    public Guid UserId { get; set; } = Guid.NewGuid();
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string MobileNumber { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>Optional spiritual name used for personalized greetings.</summary>
    public string? SeekerName { get; set; }

    /// <summary>The user's preferred region — seeds the app's region selector on sign-in.</summary>
    public Guid? DefaultRegionId { get; set; }

    public int RoleId { get; set; }
    public Role Role { get; set; } = null!;

    /// <summary>False once an administrator closes the account. Sign-in is refused and live tokens stop working.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Every access token minted before this instant is dead.
    /// </summary>
    /// <remarks>
    /// Compared against the JWT's <c>nbf</c>, which JwtTokenService already stamps, so no token in
    /// the wild has to change shape for revocation to work. Initialised to the Unix epoch rather
    /// than default(DateTime): Npgsql maps DateTime to timestamptz and refuses any value whose Kind
    /// is not Utc, and nothing sets this at registration.
    /// </remarks>
    public DateTime TokensValidFromUtc { get; set; } = DateTime.UnixEpoch;

    /// <summary>
    /// Google's stable account id (the ID token's <c>sub</c>) once the seeker has connected a Google
    /// account. Null until then. Unique where present: one Google account, one seeker.
    /// </summary>
    /// <remarks>
    /// Matched on this, never on the email, once a link exists: a Google account can change its
    /// address, and an email match is how someone who pre-registered another person's address would
    /// get in. The email is used only to DISCOVER an unlinked account, and that path asks for the
    /// account's password before linking — see AuthService.SignInWithGoogleAsync.
    /// </remarks>
    public string? GoogleSubject { get; set; }

    public DateTime? GoogleLinkedAtUtc { get; set; }

    /// <summary>
    /// When a party we trust confirmed the address. Plain registration never verifies the email, so
    /// this is null for most accounts and set the moment Google vouches for it.
    /// </summary>
    public DateTime? EmailVerifiedAtUtc { get; set; }

    // ---- data protection (DPDP Act 2023) ----

    /// <summary>The privacy notice version this seeker last accepted; null for accounts from before notices were versioned.</summary>
    public int? PrivacyNoticeVersion { get; set; }

    public DateTime? PrivacyNoticeAcceptedAtUtc { get; set; }

    /// <summary>
    /// When the seeker confirmed they are 18 or older. Accounts are for adults only: a child's data
    /// needs verifiable parental consent (section 9), which the app does not collect.
    /// </summary>
    public DateTime? AdultConfirmedAtUtc { get; set; }

    /// <summary>Optional consent to record which screens are opened, and for how long. Off by default.</summary>
    public bool UsageAnalyticsConsent { get; set; }

    /// <summary>
    /// The person the seeker nominates to exercise their rights if they die or cannot (section 14).
    /// All three are free text the seeker provides; all null means no nominee.
    /// </summary>
    public string? NomineeName { get; set; }
    public string? NomineeRelationship { get; set; }
    public string? NomineeContact { get; set; }
}
