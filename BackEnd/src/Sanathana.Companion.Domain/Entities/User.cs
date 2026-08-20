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
}
