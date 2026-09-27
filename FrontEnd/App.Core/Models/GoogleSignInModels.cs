using System.ComponentModel.DataAnnotations;
using App.Core.Common;

namespace App.Core.Models;

/// <summary>How the API answered a Google ID token. Mirrors the server's <c>GoogleOutcomes</c>.</summary>
public static class GoogleOutcomes
{
    public const string SignedIn = "SignedIn";
    public const string RegistrationRequired = "RegistrationRequired";
    public const string LinkRequired = "LinkRequired";
    public const string Rejected = "Rejected";
}

public class GoogleSignInResult
{
    public string Outcome { get; set; } = string.Empty;
    public AuthResponse? Session { get; set; }
    public string? Ticket { get; set; }
    public DateTime? TicketExpiresAtUtc { get; set; }
    public string? Email { get; set; }
    public string? FullName { get; set; }
    public string? Message { get; set; }
}

/// <summary>
/// The registration form after a first Google sign-in. There is no email field: the address is the
/// one Google verified, carried in the ticket, and the server will not read one from the body.
/// </summary>
public class GoogleRegisterRequest
{
    /// <summary>
    /// Filled in by AuthService from <see cref="Services.GoogleSignInState"/> at submit time, so it
    /// carries no validation attribute: the form validates before the ticket is copied in, and a
    /// [Required] here would refuse every submission.
    /// </summary>
    public string Ticket { get; set; } = string.Empty;

    [Required(ErrorMessage = "Full name is required.")]
    [StringLength(150)]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Mobile number is required.")]
    [RegularExpression(@"^[0-9+\-\s]{7,15}$", ErrorMessage = "Enter a valid mobile number.")]
    public string MobileNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required.")]
    [StringLength(PasswordPolicy.MaximumLength, MinimumLength = PasswordPolicy.MinimumLength,
        ErrorMessage = "Password must be 10–72 characters.")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please confirm your password.")]
    [Compare(nameof(Password), ErrorMessage = "Passwords do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [StringLength(150)]
    public string? SeekerName { get; set; }

    public Guid? RegionId { get; set; }
}

/// <summary>Connects a Google account to an existing account: the ticket plus that account's password.</summary>
public class GoogleLinkRequest
{
    public string Ticket { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
