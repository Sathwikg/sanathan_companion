using Sanathana.Companion.Application.DTOs.Auth;

namespace Sanathana.Companion.Application.Interfaces;

public interface IAuthService
{
    /// <summary>Registers a new seeker (assigned the Sanathan role). Returns a success message.</summary>
    Task<string> RegisterAsync(RegisterRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>Authenticates by email-or-mobile + password. Returns null when credentials are invalid.</summary>
    Task<AuthResponseDto?> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default);
}
