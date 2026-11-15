using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace App.Core.Auth;

/// <summary>
/// Derives the Blazor authentication state from the stored JWT. The client only reads claims
/// for UI purposes — the server validates the signature.
/// </summary>
/// <remarks>
/// With no usable session the caller is either a guest (they chose "Continue as Guest" on this
/// device) or simply anonymous. A guest is still unauthenticated — see <see cref="GuestSession"/>.
/// <para>
/// <b>Staying signed in.</b> The access token lives two hours; the refresh token stored beside it
/// lives for months and renews itself every time it is used. An expired access token is therefore
/// the normal state of a phone that was put down this morning, not a sign that the session is over.
/// This used to delete both tokens the moment it saw an expired access token, which signed every
/// seeker out whenever they opened the app after a two-hour gap. Now it renews the pair first, and
/// only a refusal from the server signs anyone out. Without a signal the seeker stays signed in on
/// the claims they already have; the first call that reaches the server renews the pair then.
/// </para>
/// </remarks>
public class JwtAuthenticationStateProvider : AuthenticationStateProvider
{
    /// <summary>
    /// How long opening the app may wait for a renewal before showing the seeker their screens anyway.
    /// The normal API timeout is generous for weak signals, which is far too long to hold a launch for.
    /// </summary>
    public static readonly TimeSpan StartupRefreshBudget = TimeSpan.FromSeconds(8);

    /// <summary>Renew a minute early rather than send a request the server will refuse on arrival.</summary>
    private static readonly TimeSpan ExpirySkew = TimeSpan.FromSeconds(60);

    private static readonly AuthenticationState Anonymous = new(new ClaimsPrincipal(new ClaimsIdentity()));
    private static readonly AuthenticationState Guest = new(GuestSession.CreatePrincipal());

    private readonly ITokenStore _tokenStore;
    private readonly IGuestModeStore _guestMode;
    private readonly TokenRefreshCoordinator? _refresh;
    private readonly IHttpClientFactory? _clients;

    /// <param name="refresh">With <paramref name="clients"/>, lets an expired access token be renewed here. Without them, an expired token keeps the session on its claims and the first API call renews it.</param>
    public JwtAuthenticationStateProvider(
        ITokenStore tokenStore,
        IGuestModeStore guestMode,
        TokenRefreshCoordinator? refresh = null,
        IHttpClientFactory? clients = null)
    {
        _tokenStore = tokenStore;
        _guestMode = guestMode;
        _refresh = refresh;
        _clients = clients;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var token = await _tokenStore.GetTokenAsync();
        var refreshToken = await _tokenStore.GetRefreshTokenAsync();

        if (string.IsNullOrWhiteSpace(token) && string.IsNullOrWhiteSpace(refreshToken))
            return await GuestOrAnonymousAsync();

        var jwt = Read(token);

        // A token that is fine for at least another minute: the everyday case, no network needed.
        if (jwt is not null && !ExpiresSoon(jwt))
            return SignedIn(jwt);

        // Expired, unreadable or missing access token. Without a refresh token there is nothing to
        // renew it with, and the session really is over.
        if (string.IsNullOrWhiteSpace(refreshToken))
            return await EndAsync();

        switch (await RenewAsync(token))
        {
            case RefreshOutcome.Renewed:
                var renewed = Read(await _tokenStore.GetTokenAsync());
                return renewed is not null ? SignedIn(renewed) : await EndAsync();

            case RefreshOutcome.Rejected:
                // The server said no. An expired session does not fall back to guest: the guest flag
                // was cleared when this seeker signed in, so they land on the login page they expect.
                return await EndAsync();

            default:
                // Could not ask: no signal, or the server is busy. Stay signed in on the claims we
                // have. The screens fetch through the API client, whose first 401 renews the pair as
                // soon as the server answers, and only a real refusal there ends the session.
                // A refresh token with no readable access token beside it (a half-finished write)
                // has no claims to show, so that seeker sees the login page until a signal returns.
                return jwt is not null ? SignedIn(jwt) : await GuestOrAnonymousAsync();
        }
    }

    private async Task<RefreshOutcome> RenewAsync(string? expiredToken)
    {
        // Built without the means to refresh (a test): leave it to the API client's first 401.
        if (_refresh is null || _clients is null) return RefreshOutcome.Unavailable;

        using var budget = new CancellationTokenSource(StartupRefreshBudget);
        try
        {
            return await _refresh.TryRefreshAsync(
                _clients.CreateClient(SessionExpiryHandler.RefreshClientName), _tokenStore, expiredToken, budget.Token);
        }
        catch (Exception)
        {
            return RefreshOutcome.Unavailable;
        }
    }

    private static JwtSecurityToken? Read(string? token)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;
        try { return new JwtSecurityTokenHandler().ReadJwtToken(token); }
        catch { return null; }
    }

    private static bool ExpiresSoon(JwtSecurityToken jwt)
        => jwt.ValidTo != default && jwt.ValidTo - ExpirySkew < DateTime.UtcNow;

    private static AuthenticationState SignedIn(JwtSecurityToken jwt)
        => new(new ClaimsPrincipal(new ClaimsIdentity(jwt.Claims, authenticationType: "jwt", nameType: "sub", roleType: ClaimTypes.Role)));

    /// <summary>Forgets a session the server will not renew, and answers as a visitor.</summary>
    private async Task<AuthenticationState> EndAsync()
    {
        await _tokenStore.ClearTokenAsync();
        return await GuestOrAnonymousAsync();
    }

    private async Task<AuthenticationState> GuestOrAnonymousAsync()
    {
        // A store that cannot be read is a visitor who has not chosen yet: show them the login page.
        try { return await _guestMode.IsGuestAsync() ? Guest : Anonymous; }
        catch { return Anonymous; }
    }

    public void NotifyAuthenticationChanged()
        => NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
}
