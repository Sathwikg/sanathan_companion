namespace App.Core.Auth;

/// <summary>Per-platform credential storage (web = localStorage, mobile = SecureStorage).</summary>
/// <remarks>
/// Two tokens, always written and cleared together. The access token is short-lived and goes on
/// every request; the refresh token is long-lived, opaque, and used once — see
/// <see cref="TokenRefreshCoordinator"/>. Storing one without the other produces a session that
/// cannot renew itself or one that renews after sign-out, so there is no setter for either alone.
/// </remarks>
public interface ITokenStore
{
    Task<string?> GetTokenAsync();

    Task<string?> GetRefreshTokenAsync();

    Task SetTokensAsync(string accessToken, string refreshToken);

    /// <summary>Forgets both.</summary>
    Task ClearTokenAsync();
}
