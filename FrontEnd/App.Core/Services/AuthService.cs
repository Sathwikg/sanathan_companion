using App.Core.Auth;
using App.Core.Models;

namespace App.Core.Services;

public class AuthService : IAuthService
{
    private readonly IApiClient _api;
    private readonly ITokenStore _tokenStore;
    private readonly JwtAuthenticationStateProvider _authProvider;
    private readonly IEnumerable<IUserSessionState> _userState;
    private readonly SessionExpiredNotifier _sessionExpired;
    private readonly TokenRefreshCoordinator _refresh;
    private readonly GoogleSignInState _google;

    public AuthService(
        IApiClient api,
        ITokenStore tokenStore,
        JwtAuthenticationStateProvider authProvider,
        IEnumerable<IUserSessionState> userState,
        SessionExpiredNotifier sessionExpired,
        TokenRefreshCoordinator refresh,
        GoogleSignInState google)
    {
        _api = api;
        _tokenStore = tokenStore;
        _authProvider = authProvider;
        _userState = userState;
        _sessionExpired = sessionExpired;
        _refresh = refresh;
        _google = google;
    }

    /// <summary>Drops every per-user cache so one account's data is never shown to the next.</summary>
    private void ResetUserState()
    {
        foreach (var state in _userState) state.Reset();
    }

    public Task<(bool Success, string Message)> RegisterAsync(RegisterRequest request)
        => _api.RegisterAsync(request);

    public async Task<(bool Success, string Error)> LoginAsync(LoginRequest request)
    {
        var (success, data, error) = await _api.LoginAsync(request);
        if (!success || data is null)
            return (false, string.IsNullOrWhiteSpace(error) ? "Login failed." : error);

        await BeginSessionAsync(data);
        return (true, string.Empty);
    }

    public async Task<(bool Success, string Outcome, string Error)> SignInWithGoogleAsync(string idToken)
    {
        var (success, data, error) = await _api.SignInWithGoogleAsync(idToken);
        if (!success || data is null)
            return (false, string.Empty, string.IsNullOrWhiteSpace(error) ? "Google sign-in failed." : error);

        switch (data.Outcome)
        {
            case GoogleOutcomes.SignedIn when data.Session is not null:
                _google.Clear();
                await BeginSessionAsync(data.Session);
                return (true, data.Outcome, string.Empty);

            case GoogleOutcomes.RegistrationRequired:
            case GoogleOutcomes.LinkRequired:
                if (string.IsNullOrWhiteSpace(data.Ticket))
                    return (false, data.Outcome, "Google sign-in failed.");
                _google.Set(data.Ticket, data.TicketExpiresAtUtc, data.Email, data.FullName);
                return (true, data.Outcome, string.Empty);

            default:
                return (false, data.Outcome, string.IsNullOrWhiteSpace(data.Message) ? "Google sign-in failed." : data.Message);
        }
    }

    public async Task<(bool Success, string Error)> RegisterWithGoogleAsync(GoogleRegisterRequest request)
    {
        if (!_google.IsUsable)
            return (false, "Your Google sign-in has expired. Please try again.");

        request.Ticket = _google.Ticket!;
        var (success, data, error) = await _api.RegisterWithGoogleAsync(request);
        if (!success || data is null)
            return (false, string.IsNullOrWhiteSpace(error) ? "Registration failed." : error);

        _google.Clear();
        await BeginSessionAsync(data);
        return (true, string.Empty);
    }

    public async Task<(bool Success, string Error)> LinkGoogleAsync(string password)
    {
        if (!_google.IsUsable)
            return (false, "Your Google sign-in has expired. Please try again.");

        var (success, data, error) = await _api.LinkGoogleAsync(
            new GoogleLinkRequest { Ticket = _google.Ticket!, Password = password }, _google.Email ?? string.Empty);
        if (!success || data is null)
            return (false, string.IsNullOrWhiteSpace(error) ? "Could not connect your Google account." : error);

        _google.Clear();
        await BeginSessionAsync(data);
        return (true, string.Empty);
    }

    /// <summary>Stores a freshly issued session and wakes the app up as the new user.</summary>
    private async Task BeginSessionAsync(AuthResponse data)
    {
        // Token first, then reset. A reset can start a refetch — LocalizationState does — and it
        // has to carry the new token, not the old one or none. Still before
        // NotifyAuthenticationChanged, which is what actually re-renders the app, so no stale
        // cache is ever on screen.
        await _tokenStore.SetTokensAsync(data.Token, data.RefreshToken);
        _refresh.Reset();
        ResetUserState();
        // Re-arm the expiry latch, or the first 401 of the PREVIOUS session would still be
        // silencing the next one.
        _sessionExpired.Reset();
        _authProvider.NotifyAuthenticationChanged();
    }

    public async Task LogoutAsync()
    {
        // Tell the server first, while the refresh token is still readable. Best effort: the
        // family expires on its own, and a seeker offline on their own phone must still be able to
        // sign out of it.
        var refreshToken = await _tokenStore.GetRefreshTokenAsync();
        if (!string.IsNullOrWhiteSpace(refreshToken))
            await _api.LogoutAsync(refreshToken);

        // Same ordering argument as sign-in: clear the tokens first so a reset-triggered refetch
        // runs as an anonymous caller and gets the anonymous answer.
        await _tokenStore.ClearTokenAsync();
        _refresh.Reset();
        _google.Clear();
        ResetUserState();
        _authProvider.NotifyAuthenticationChanged();
    }

    /// <summary>
    /// Stores the pair a password change hands back, so the device that changed the password stays
    /// signed in while every other one is signed out.
    /// </summary>
    public async Task AdoptAsync(AuthResponse data)
    {
        await _tokenStore.SetTokensAsync(data.Token, data.RefreshToken);
        _refresh.Reset();
        _sessionExpired.Reset();
    }
}
