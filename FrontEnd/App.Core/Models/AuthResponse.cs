namespace App.Core.Models;

public class AuthResponse
{
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; set; }

    /// <summary>Opaque and single-use. Buys a new access token once this one expires.</summary>
    public string RefreshToken { get; set; } = string.Empty;

    public DateTime RefreshExpiresAtUtc { get; set; }
    public Guid UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? SeekerName { get; set; }
    public string Role { get; set; } = string.Empty;
}

/// <summary>Body of the refresh and sign-out calls.</summary>
public class RefreshRequest
{
    public string RefreshToken { get; set; } = string.Empty;
}

/// <summary>A media ticket and the moment it stops being accepted.</summary>
public class MediaTicket
{
    public string Ticket { get; set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; set; }
}
