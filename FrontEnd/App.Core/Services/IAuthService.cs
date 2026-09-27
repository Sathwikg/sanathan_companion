using App.Core.Models;

namespace App.Core.Services;

public interface IAuthService
{
    Task<(bool Success, string Message)> RegisterAsync(RegisterRequest request);
    Task<(bool Success, string Error)> LoginAsync(LoginRequest request);
    Task LogoutAsync();

    /// <summary>
    /// Presents a Google ID token. On <see cref="Models.GoogleOutcomes.SignedIn"/> the session is
    /// stored; on RegistrationRequired or LinkRequired the ticket is parked in
    /// <see cref="GoogleSignInState"/> for the page that finishes the job.
    /// </summary>
    Task<(bool Success, string Outcome, string Error)> SignInWithGoogleAsync(string idToken);

    /// <summary>Finishes a Google registration with the parked ticket and stores the session.</summary>
    Task<(bool Success, string Error)> RegisterWithGoogleAsync(GoogleRegisterRequest request);

    /// <summary>Connects Google to the existing account with the parked ticket and stores the session.</summary>
    Task<(bool Success, string Error)> LinkGoogleAsync(string password);

    /// <summary>Adopts a token pair the server issued outside sign-in — see the password change.</summary>
    Task AdoptAsync(AuthResponse data);
}
