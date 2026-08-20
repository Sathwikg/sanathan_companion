using System.Net.Http.Json;
using App.Core.Config;
using App.Core.Models;

namespace App.Core.Auth;

/// <summary>
/// Turns many simultaneous "my token expired" discoveries into one refresh.
/// </summary>
/// <remarks>
/// A dashboard fires half a dozen requests at once, and on a phone they all come back 401 together
/// after the app has been asleep. Refreshing once per 401 would present the same refresh token six
/// times — and the server revokes the whole family on a reused token, because from its side a
/// replay is indistinguishable from a theft. So the first caller refreshes and the rest wait.
/// <para>
/// Registered as a SINGLETON. Not because concurrent requests would otherwise get separate
/// instances — IHttpClientFactory builds one handler chain and every request shares it — but
/// because it rotates that chain, and its scope, on a timer. A scoped coordinator would let a
/// refresh started on the old chain race one started on the new chain, which is precisely the
/// double-presentation this class exists to prevent.
/// </para>
/// </remarks>
public class TokenRefreshCoordinator
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>The refresh token that was last exchanged, so a queued caller does not re-spend it.</summary>
    private string? _lastExchanged;

    /// <summary>
    /// Ensures the stored access token is not the one the caller just saw rejected.
    /// </summary>
    /// <param name="http">
    /// The bare client. Deliberately not the typed <c>IApiClient</c>: that one runs through this
    /// very handler chain, so refreshing through it would recurse.
    /// </param>
    /// <param name="rejectedToken">The access token the 401 came back for, or null if there was none.</param>
    /// <returns>True when a usable token is now stored.</returns>
    public async Task<bool> TryRefreshAsync(HttpClient http, ITokenStore store, string? rejectedToken, CancellationToken cancellationToken)
    {
        var refreshToken = await store.GetRefreshTokenAsync();
        if (string.IsNullOrWhiteSpace(refreshToken)) return false;

        await _gate.WaitAsync(cancellationToken);
        try
        {
            // Somebody refreshed while this caller queued. The stored access token is already a
            // different one, so there is nothing to do but let them retry with it.
            var current = await store.GetTokenAsync();
            if (!string.IsNullOrWhiteSpace(current) && current != rejectedToken)
                return true;

            refreshToken = await store.GetRefreshTokenAsync();
            if (string.IsNullOrWhiteSpace(refreshToken) || refreshToken == _lastExchanged)
                return false;

            var response = await http.PostAsJsonAsync(
                ApiRoutes.Auth.Refresh,
                new RefreshRequest { RefreshToken = refreshToken },
                cancellationToken);

            if (!response.IsSuccessStatusCode) return false;

            var renewed = await response.Content.ReadFromJsonAsync<AuthResponse>(cancellationToken);
            if (renewed is null || string.IsNullOrWhiteSpace(renewed.Token)) return false;

            _lastExchanged = refreshToken;
            await store.SetTokensAsync(renewed.Token, renewed.RefreshToken);
            return true;
        }
        catch
        {
            // Offline, or the server is down. Not an expired session — the caller's original 401
            // still stands and will be reported as one.
            return false;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Forgets the last exchange, so a new session starts clean.</summary>
    public void Reset() => _lastExchanged = null;
}
