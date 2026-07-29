using System.Net.Http.Headers;

namespace App.Core.Auth;

/// <summary>Attaches the stored JWT as a Bearer header on every outgoing API request.</summary>
public class BearerTokenHandler : DelegatingHandler
{
    private readonly ITokenStore _tokenStore;

    public BearerTokenHandler(ITokenStore tokenStore) => _tokenStore = tokenStore;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = await _tokenStore.GetTokenAsync();
        if (!string.IsNullOrWhiteSpace(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return await base.SendAsync(request, cancellationToken);
    }
}
