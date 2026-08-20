namespace Sanathana.Companion.Infrastructure.Identity;

public class JwtSettings
{
    public const string SectionName = "JwtSettings";

    public string Secret { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public int ExpiryMinutes { get; set; } = 120;

    /// <summary>
    /// How long a refresh token lives. Read only by RefreshTokenFactory — the Application layer
    /// cannot see this class, which is why the lifetime is exposed through that interface.
    /// </summary>
    public int RefreshTokenDays { get; set; } = 30;
}
