using System.Net;
using System.Net.Http.Json;
using App.Core.Config;
using App.Core.Models;

namespace App.Core.Auth;

/// <summary>What a refresh attempt established.</summary>
public enum RefreshOutcome
{
    /// <summary>A usable access token is stored (this caller refreshed, or another caller just did).</summary>
    Renewed,

    /// <summary>
    /// The server looked at the refresh token and said no: it expired, was revoked by a sign-out
    /// elsewhere or a password change, or the account was closed. The session is over.
    /// </summary>
    Rejected,

    /// <summary>
    /// Nobody could say: no signal, a timeout, the API restarting behind the proxy, a rate limit.
    /// The session is NOT over; the next request simply tries again.
    /// </summary>
    Unavailable
}

/// <summary>
/// Turns many simultaneous "my token expired" discoveries into one refresh.
/// </summary>
/// <remarks>
/// A dashboard fires half a dozen requests at once, and on a phone they all come back 401 together
/// after the app has been asleep. Refreshing once per 401 would present the same refresh token six
/// times, and the server treats a token presented after its successor has been used as stolen.
/// So the first caller refreshes and the rest wait.
/// <para>
/// The answer has three values, not two, and that is the whole of "stay signed in like Instagram":
/// only <see cref="RefreshOutcome.Rejected"/> may end a session. Treating "could not reach the
/// server" as "the server refused" signed seekers out every time they opened the app on a train,
/// and every time the API was redeployed.
/// </para>
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
    /// Ensures the stored access token is not the one the caller just saw rejected (or found expired).
    /// </summary>
    /// <param name="http">
    /// The bare client. Deliberately not the typed <c>IApiClient</c>: that one runs through the
    /// session-expiry handler, so refreshing through it would recurse.
    /// </param>
    /// <param name="rejectedToken">The access token that failed, or null when there was none.</param>
    public async Task<RefreshOutcome> TryRefreshAsync(HttpClient http, ITokenStore store, string? rejectedToken, CancellationToken cancellationToken)
    {
        var refreshToken = await store.GetRefreshTokenAsync();
        if (string.IsNullOrWhiteSpace(refreshToken)) return RefreshOutcome.Rejected;

        try
        {
            await _gate.WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return RefreshOutcome.Unavailable;
        }

        try
        {
            // Somebody refreshed while this caller queued. The stored access token is already a
            // different one, so there is nothing to do but let them retry with it.
            var current = await store.GetTokenAsync();
            if (!string.IsNullOrWhiteSpace(current) && current != rejectedToken)
                return RefreshOutcome.Renewed;

            refreshToken = await store.GetRefreshTokenAsync();
            if (string.IsNullOrWhiteSpace(refreshToken)) return RefreshOutcome.Rejected;

            // This exact refresh token was already exchanged by this process, and the token it
            // bought is the one that just failed: the server accepted the refresh and then refused
            // what it issued (the account was closed in between). Asking again would only present a
            // spent token.
            if (refreshToken == _lastExchanged) return RefreshOutcome.Rejected;

            HttpResponseMessage response;
            try
            {
                response = await http.PostAsJsonAsync(
                    ApiRoutes.Auth.Refresh,
                    new RefreshRequest { RefreshToken = refreshToken },
                    cancellationToken);
            }
            catch (Exception)
            {
                // Offline, DNS, TLS, a timeout or a cancelled wait. Says nothing about the session.
                return RefreshOutcome.Unavailable;
            }

            using (response)
            {
                if (IsRefusal(response.StatusCode)) return RefreshOutcome.Rejected;
                if (!response.IsSuccessStatusCode) return RefreshOutcome.Unavailable;

                AuthResponse? renewed;
                try { renewed = await response.Content.ReadFromJsonAsync<AuthResponse>(cancellationToken); }
                catch (Exception) { return RefreshOutcome.Unavailable; }   // a proxy's HTML error page, a cut-off body

                if (renewed is null || string.IsNullOrWhiteSpace(renewed.Token) || string.IsNullOrWhiteSpace(renewed.RefreshToken))
                    return RefreshOutcome.Unavailable;

                _lastExchanged = refreshToken;
                await store.SetTokensAsync(renewed.Token, renewed.RefreshToken);
                return RefreshOutcome.Renewed;
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// The answers that mean "this refresh token is finished". Everything else that is not a success
    /// (429 from the rate limiter, 502/503 while the API restarts, 408, 500) means "try later".
    /// </summary>
    public static bool IsRefusal(HttpStatusCode status)
        => status is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.BadRequest;

    /// <summary>Forgets the last exchange, so a new session starts clean.</summary>
    public void Reset() => _lastExchanged = null;
}
