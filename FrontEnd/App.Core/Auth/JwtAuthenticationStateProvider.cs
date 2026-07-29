using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace App.Core.Auth;

/// <summary>
/// Derives the Blazor authentication state from the stored JWT. The client only reads claims
/// for UI purposes — the server validates the signature.
/// </summary>
public class JwtAuthenticationStateProvider : AuthenticationStateProvider
{
    private static readonly AuthenticationState Anonymous = new(new ClaimsPrincipal(new ClaimsIdentity()));
    private readonly ITokenStore _tokenStore;

    public JwtAuthenticationStateProvider(ITokenStore tokenStore) => _tokenStore = tokenStore;

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var token = await _tokenStore.GetTokenAsync();
        if (string.IsNullOrWhiteSpace(token))
            return Anonymous;

        try
        {
            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
            if (jwt.ValidTo != default && jwt.ValidTo < DateTime.UtcNow)
            {
                await _tokenStore.ClearTokenAsync();
                return Anonymous;
            }

            var identity = new ClaimsIdentity(jwt.Claims, authenticationType: "jwt", nameType: "sub", roleType: ClaimTypes.Role);
            return new AuthenticationState(new ClaimsPrincipal(identity));
        }
        catch
        {
            await _tokenStore.ClearTokenAsync();
            return Anonymous;
        }
    }

    public void NotifyAuthenticationChanged()
        => NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
}
