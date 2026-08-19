using System.Net.Http.Headers;
using App.Core.Config;

namespace App.Core.Auth;

/// <summary>Attaches the stored JWT as a Bearer header on every outgoing API request.</summary>
public class BearerTokenHandler : DelegatingHandler
{
    private readonly ITokenStore _tokenStore;

    public BearerTokenHandler(ITokenStore tokenStore) => _tokenStore = tokenStore;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // The auth endpoints are anonymous, and a stale token has no business on them. It also
        // matters for the session-expiry handler outside this one: it treats "401 on a request that
        // carried a credential" as the session ending, so a mistyped password — which is a 401 from
        // auth/login — would otherwise sign the seeker out of the session they were trying to start
        // and replace "Invalid email/mobile or password" with "Your session has ended".
        if (!IsAnonymous(request.RequestUri))
        {
            var token = await _tokenStore.GetTokenAsync();
            if (!string.IsNullOrWhiteSpace(token))
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await base.SendAsync(request, cancellationToken);
    }

    /// <summary>
    /// Endpoints that never take a credential. Matched on the trailing path so it holds however the
    /// API base URL is configured — "/api", an absolute host, or a proxied path.
    /// </summary>
    private static bool IsAnonymous(Uri? uri)
    {
        if (uri is null) return false;

        var path = uri.IsAbsoluteUri ? uri.AbsolutePath : uri.OriginalString;

        return path.EndsWith(ApiRoutes.Auth.Login, StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(ApiRoutes.Auth.Register, StringComparison.OrdinalIgnoreCase);
    }
}
