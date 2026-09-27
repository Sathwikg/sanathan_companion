using Sanathana.Companion.Application.DTOs.Auth;

namespace Sanathana.Companion.Application.Interfaces;

public interface IAuthService
{
    /// <summary>Registers a new seeker (assigned the Sanathan role). Returns a success message.</summary>
    Task<string> RegisterAsync(RegisterRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>Authenticates by email-or-mobile + password. Returns null when credentials are invalid.</summary>
    Task<AuthResponseDto?> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Exchanges a refresh token for a fresh pair. Returns null when the token is unknown, expired,
    /// already used, or belongs to an account that has since been closed.
    /// </summary>
    Task<AuthResponseDto?> RefreshAsync(RefreshRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes the whole family a refresh token belongs to. Silent about whether it existed.
    /// </summary>
    Task LogoutAsync(RefreshRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Changes the given user's own password and returns a fresh token pair, so the device that
    /// asked stays signed in while every other device is signed out. Returns null when the current
    /// password is wrong, which the caller should surface as a 400 rather than a 401 — the session
    /// is still valid.
    /// </summary>
    Task<AuthResponseDto?> ChangePasswordAsync(Guid userId, ChangePasswordDto request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifies a Google ID token and decides what happens next: a session when the Google account
    /// is already connected, a registration ticket when the address is new, or a link ticket when an
    /// account with that address exists and has to consent with its password. Returns null when the
    /// token does not verify, which the caller surfaces as a 401 without saying why.
    /// </summary>
    Task<GoogleSignInResultDto?> SignInWithGoogleAsync(GoogleSignInDto request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates the account a registration ticket was issued for and signs it in. The email is the
    /// ticket's; the password is chosen here so email + password works for this account too.
    /// </summary>
    Task<AuthResponseDto> RegisterWithGoogleAsync(GoogleRegisterDto request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Connects the Google account in a link ticket to the existing account holding its address, once
    /// that account's password has been typed. Returns null when the password is wrong (a 400: the
    /// ticket is fine and may be retried).
    /// </summary>
    Task<AuthResponseDto?> LinkGoogleAsync(GoogleLinkDto request, CancellationToken cancellationToken = default);
}
