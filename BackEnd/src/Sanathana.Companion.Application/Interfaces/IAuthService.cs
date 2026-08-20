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
}
