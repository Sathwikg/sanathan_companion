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

    public AuthService(
        IApiClient api,
        ITokenStore tokenStore,
        JwtAuthenticationStateProvider authProvider,
        IEnumerable<IUserSessionState> userState,
        SessionExpiredNotifier sessionExpired)
    {
        _api = api;
        _tokenStore = tokenStore;
        _authProvider = authProvider;
        _userState = userState;
        _sessionExpired = sessionExpired;
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

        // Token first, then reset. A reset can start a refetch — LocalizationState does — and it
        // has to carry the new token, not the old one or none. Still before
        // NotifyAuthenticationChanged, which is what actually re-renders the app, so no stale
        // cache is ever on screen.
        await _tokenStore.SetTokenAsync(data.Token);
        ResetUserState();
        // Re-arm the expiry latch, or the first 401 of the PREVIOUS session would still be
        // silencing the next one.
        _sessionExpired.Reset();
        _authProvider.NotifyAuthenticationChanged();
        return (true, string.Empty);
    }

    public async Task LogoutAsync()
    {
        // Same ordering argument as sign-in: clear the token first so a reset-triggered refetch
        // runs as an anonymous caller and gets the anonymous answer.
        await _tokenStore.ClearTokenAsync();
        ResetUserState();
        _authProvider.NotifyAuthenticationChanged();
    }
}
