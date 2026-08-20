using System.Net;
using System.Net.Http.Headers;

namespace App.Core.Auth;

/// <summary>
/// Renews a dead credential if it can, and ends the session if it cannot.
/// </summary>
/// <remarks>
/// Without this the app has no idea its credential died. On mobile — where the process survives
/// days of backgrounding — the access token is expired on nearly every resume. The UI would still
/// believe it was signed in (the cascading auth state is cached until something calls
/// NotifyAuthenticationStateChanged, and nothing did), so every screen would fail on its own: some
/// showing "Response status code does not indicate success: 401", and the ones whose loader flag is
/// cleared only after the awaited call simply spinning forever.
/// <para>
/// The refresh attempt sits here rather than in the API client because this is the only place that
/// sees the 401 for every call, including the ones that do not go through a named method.
/// </para>
/// </remarks>
public class SessionExpiryHandler : DelegatingHandler
{
    private readonly ITokenStore _tokenStore;
    private readonly SessionExpiredNotifier _notifier;
    private readonly TokenRefreshCoordinator _refresh;
    private readonly IHttpClientFactory _clients;

    public SessionExpiryHandler(
        ITokenStore tokenStore,
        SessionExpiredNotifier notifier,
        TokenRefreshCoordinator refresh,
        IHttpClientFactory clients)
    {
        _tokenStore = tokenStore;
        _notifier = notifier;
        _refresh = refresh;
        _clients = clients;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode != HttpStatusCode.Unauthorized)
            return response;

        // Only a request that actually carried a credential tells us the SESSION expired. A 401 on
        // an anonymous call is just a failed sign-in, and signing the user out over it would log
        // them out of a session they never had.
        var presented = request.Headers.Authorization?.Parameter;
        if (presented is null)
            return response;

        var renewed = await _refresh.TryRefreshAsync(
            _clients.CreateClient(RefreshClientName), _tokenStore, presented, cancellationToken);

        if (!renewed)
        {
            _notifier.Raise();
            return response;
        }

        // Replay with the token that was just minted. The original response is disposed because
        // nothing will read it and it holds a socket until it is.
        response.Dispose();

        var token = await _tokenStore.GetTokenAsync();
        request.Headers.Authorization = string.IsNullOrWhiteSpace(token)
            ? null
            : new AuthenticationHeaderValue("Bearer", token);

        var replay = await base.SendAsync(request, cancellationToken);

        // A refresh that succeeded but bought nothing — the account was closed between the two
        // calls, say — must still end the session. Handing a bare 401 back to the caller is the
        // exact failure this class exists to prevent.
        if (replay.StatusCode == HttpStatusCode.Unauthorized && request.Headers.Authorization is not null)
            _notifier.Raise();

        return replay;
    }

    /// <summary>
    /// The client used for the refresh call itself: a bare one, with no handler chain.
    /// </summary>
    /// <remarks>
    /// Refreshing through the typed API client would send the request back through this very
    /// handler, so a 401 on the refresh would try to refresh, forever.
    /// </remarks>
    public const string RefreshClientName = "sc-refresh";
}
