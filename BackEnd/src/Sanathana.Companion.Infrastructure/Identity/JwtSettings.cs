namespace Sanathana.Companion.Infrastructure.Identity;

public class JwtSettings
{
    public const string SectionName = "JwtSettings";

    public string Secret { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public int ExpiryMinutes { get; set; } = 120;

    /// <summary>
    /// How long a refresh token lives in a browser. Read only by RefreshTokenFactory — the
    /// Application layer cannot see this class, which is why the lifetime is exposed through that
    /// interface. Every refresh issues a new token with a fresh lifetime, so this is the longest a
    /// seeker may go WITHOUT opening the site before being asked to sign in again.
    /// </summary>
    public int RefreshTokenDays { get; set; } = 30;

    /// <summary>
    /// The same for the phone app, which a seeker expects to stay signed in until they sign out,
    /// the way every app on their phone does. Sliding like the browser's, so only a full year without
    /// opening the app ends the session. Chosen by the X-Platform header the app sends.
    /// </summary>
    public int MobileRefreshTokenDays { get; set; } = 365;
}
