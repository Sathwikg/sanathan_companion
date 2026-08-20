namespace Sanathana.Companion.Application.DTOs.Auth;

public class AuthResponseDto
{
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; set; }

    /// <summary>Opaque, single-use. Buys a new access token once this one expires.</summary>
    public string RefreshToken { get; set; } = string.Empty;

    public DateTime RefreshExpiresAtUtc { get; set; }
    public Guid UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? SeekerName { get; set; }
    public string Role { get; set; } = string.Empty;
}
