using App.Core.Services;

namespace App.Core.Auth;

/// <summary>
/// Stamps X-Platform on every API request, so the server's session and page-visit logs can say
/// which device family a sign-in came from. Informational only: nothing authorizes on it.
/// </summary>
public class PlatformHeaderHandler : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Headers.TryAddWithoutValidation(ClientPlatform.Header, ClientPlatform.Name);
        return base.SendAsync(request, cancellationToken);
    }
}
