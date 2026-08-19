using System.Net;

namespace App.Core.Auth;

/// <summary>
/// Turns a 401 into an actual end of session.
/// </summary>
/// <remarks>
/// Without this the app has no idea its credential died. The JWT lasts 120 minutes and there is no
/// refresh, so on mobile — where the process survives days of backgrounding — the token is expired
/// on nearly every resume. The UI would still believe it was signed in (the cascading auth state is
/// cached until something calls NotifyAuthenticationStateChanged, and nothing did), so every screen
/// would fail on its own: some showing "Response status code does not indicate success: 401", and
/// the ones whose loader flag is cleared only after the awaited call simply spinning forever.
/// </remarks>
public class SessionExpiryHandler : DelegatingHandler
{
    private readonly ITokenStore _tokenStore;
    private readonly SessionExpiredNotifier _notifier;

    public SessionExpiryHandler(ITokenStore tokenStore, SessionExpiredNotifier notifier)
    {
        _tokenStore = tokenStore;
        _notifier = notifier;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode != HttpStatusCode.Unauthorized)
            return response;

        // Only a request that actually carried a credential tells us the SESSION expired. A 401 on
        // an anonymous call is just a failed sign-in, and signing the user out over it would log
        // them out of a session they never had.
        if (request.Headers.Authorization is not null)
            _notifier.Raise();

        return response;
    }
}
