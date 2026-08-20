using Sanathana.Companion.Application.DTOs.Auth;

namespace Sanathana.Companion.Application.Interfaces;

public interface IAuthService
{
    /// <summary>Registers a new seeker (assigned the Sanathan role). Returns a success message.</summary>
    Task<string> RegisterAsync(RegisterRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>Authenticates by email-or-mobile + password. Returns null when credentials are invalid.</summary>
    Task<AuthResponseDto?> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Changes the given user's own password. Returns false when the current password is wrong,
    /// which the caller should surface as a 400 rather than a 401 — the session is still valid.
    /// </summary>
    Task<bool> ChangePasswordAsync(Guid userId, ChangePasswordDto request, CancellationToken cancellationToken = default);
}
