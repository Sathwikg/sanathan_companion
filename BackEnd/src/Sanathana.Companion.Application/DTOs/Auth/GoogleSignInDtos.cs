namespace Sanathana.Companion.Application.DTOs.Auth;

/// <summary>The Google ID token a client obtained from Google Identity Services or Credential Manager.</summary>
public class GoogleSignInDto
{
    public string IdToken { get; set; } = string.Empty;
}

/// <summary>How a Google sign-in ended. The client branches on <see cref="GoogleSignInResultDto.Outcome"/>.</summary>
public static class GoogleOutcomes
{
    /// <summary>The Google account is already connected to an open account; a session was issued.</summary>
    public const string SignedIn = "SignedIn";

    /// <summary>Nobody has this email yet; the seeker completes the registration form with the ticket.</summary>
    public const string RegistrationRequired = "RegistrationRequired";

    /// <summary>An account with this email exists but is not connected; its password confirms the link.</summary>
    public const string LinkRequired = "LinkRequired";

    /// <summary>The token was fine but the account may not sign in: closed, for instance.</summary>
    public const string Rejected = "Rejected";
}

public class GoogleSignInResultDto
{
    public string Outcome { get; set; } = string.Empty;

    /// <summary>Set for <see cref="GoogleOutcomes.SignedIn"/> only.</summary>
    public AuthResponseDto? Session { get; set; }

    /// <summary>Set for RegistrationRequired and LinkRequired: proof of the verified identity for the next call.</summary>
    public string? Ticket { get; set; }
    public DateTime? TicketExpiresAtUtc { get; set; }

    /// <summary>The verified address, so the client can show it. It is read from the ticket server-side, never from the form.</summary>
    public string? Email { get; set; }

    /// <summary>Google's display name, offered as the default full name on the registration form.</summary>
    public string? FullName { get; set; }

    /// <summary>Set for <see cref="GoogleOutcomes.Rejected"/>.</summary>
    public string? Message { get; set; }
}

/// <summary>The registration form as completed after a first Google sign-in. The email comes from the ticket.</summary>
public class GoogleRegisterDto : IRegistrationFields
{
    public string Ticket { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string MobileNumber { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
    public string? SeekerName { get; set; }
    public Guid? RegionId { get; set; }
}

/// <summary>Connects a Google account to an existing account, on the strength of that account's password.</summary>
public class GoogleLinkDto
{
    public string Ticket { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
