using App.Core.Models;

namespace App.Core.Services;

public interface IAuthService
{
    Task<(bool Success, string Message)> RegisterAsync(RegisterRequest request);
    Task<(bool Success, string Error)> LoginAsync(LoginRequest request);
    Task LogoutAsync();

    /// <summary>Adopts a token pair the server issued outside sign-in — see the password change.</summary>
    Task AdoptAsync(AuthResponse data);
}
