using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace App.Core.Auth;

/// <summary>
/// Derives the Blazor authentication state from the stored JWT. The client only reads claims
/// for UI purposes — the server validates the signature.
/// </summary>
/// <remarks>
/// With no usable token the caller is either a guest (they chose "Continue as Guest" on this
/// device) or simply anonymous. A guest is still unauthenticated — see <see cref="GuestSession"/>.
/// </remarks>
public class JwtAuthenticationStateProvider : AuthenticationStateProvider
{
    private static readonly AuthenticationState Anonymous = new(new ClaimsPrincipal(new ClaimsIdentity()));
    private static readonly AuthenticationState Guest = new(GuestSession.CreatePrincipal());
    private readonly ITokenStore _tokenStore;
    private readonly IGuestModeStore _guestMode;

    public JwtAuthenticationStateProvider(ITokenStore tokenStore, IGuestModeStore guestMode)
    {
        _tokenStore = tokenStore;
        _guestMode = guestMode;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var token = await _tokenStore.GetTokenAsync();
        if (string.IsNullOrWhiteSpace(token))
            return await GuestOrAnonymousAsync();

        try
        {
            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
            if (jwt.ValidTo != default && jwt.ValidTo < DateTime.UtcNow)
            {
                // An expired session does not fall back to guest: the guest flag was cleared when
                // this seeker signed in, so they land on the login page they expect.
                await _tokenStore.ClearTokenAsync();
                return await GuestOrAnonymousAsync();
            }

            var identity = new ClaimsIdentity(jwt.Claims, authenticationType: "jwt", nameType: "sub", roleType: ClaimTypes.Role);
            return new AuthenticationState(new ClaimsPrincipal(identity));
        }
        catch
        {
            await _tokenStore.ClearTokenAsync();
            return await GuestOrAnonymousAsync();
        }
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
