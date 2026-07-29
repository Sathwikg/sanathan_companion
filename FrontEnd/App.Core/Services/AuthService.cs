using App.Core.Auth;
using App.Core.Models;

namespace App.Core.Services;

public class AuthService : IAuthService
{
    private readonly IApiClient _api;
    private readonly ITokenStore _tokenStore;
    private readonly JwtAuthenticationStateProvider _authProvider;
    private readonly IEnumerable<IUserSessionState> _userState;

    public AuthService(
        IApiClient api,
        ITokenStore tokenStore,
        JwtAuthenticationStateProvider authProvider,
        IEnumerable<IUserSessionState> userState)
    {
        _api = api;
        _tokenStore = tokenStore;
        _authProvider = authProvider;
        _userState = userState;
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

        ResetUserState();
        await _tokenStore.SetTokenAsync(data.Token);
        _authProvider.NotifyAuthenticationChanged();
        return (true, string.Empty);
    }

    public async Task LogoutAsync()
    {
        ResetUserState();
        await _tokenStore.ClearTokenAsync();
        _authProvider.NotifyAuthenticationChanged();
    }
}
