using System.Net;
using App.Core.Auth;
using App.Core.Config;

namespace App.Tests;

/// <summary>
/// The two handlers that wrap every API call. Together they decide whether a request carries a
/// credential and whether a 401 ends the session, so the interaction between them is pinned here.
/// </summary>
public class AuthPipelineTests
{
    private sealed class StubTokenStore : ITokenStore
    {
        public string? Token { get; set; } = "stored-jwt";
        public Task<string?> GetTokenAsync() => Task.FromResult(Token);
        public Task SetTokenAsync(string token) { Token = token; return Task.CompletedTask; }
        public Task ClearTokenAsync() { Token = null; return Task.CompletedTask; }
    }

    /// <summary>Terminates the pipeline with a fixed status and records what it was sent.</summary>
    private sealed class CapturingHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        public HttpRequestMessage? Seen { get; private set; }

        public CapturingHandler(HttpStatusCode status) => _status = status;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Seen = request;
            return Task.FromResult(new HttpResponseMessage(_status));
        }
    }

    private static (HttpClient Client, CapturingHandler Inner, SessionExpiredNotifier Notifier) Pipeline(
        HttpStatusCode status, StubTokenStore? store = null)
    {
        store ??= new StubTokenStore();
        var notifier = new SessionExpiredNotifier();
        var inner = new CapturingHandler(status);

        // Mirrors AddAppCore: the expiry handler is OUTERMOST, wrapping the bearer handler, so it
        // observes the Authorization header the inner handler added.
        var bearer = new BearerTokenHandler(store) { InnerHandler = inner };
        var expiry = new SessionExpiryHandler(store, notifier) { InnerHandler = bearer };

        return (new HttpClient(expiry) { BaseAddress = new Uri("https://example.test/api/") }, inner, notifier);
    }

    [Fact]
    public async Task A_normal_request_carries_the_stored_token()
    {
        var (client, inner, _) = Pipeline(HttpStatusCode.OK);

        await client.GetAsync(ApiRoutes.Dashboard.Mine);

        Assert.Equal("Bearer", inner.Seen!.Headers.Authorization!.Scheme);
        Assert.Equal("stored-jwt", inner.Seen.Headers.Authorization.Parameter);
    }

    [Fact]
    public async Task A_401_on_an_authenticated_request_ends_the_session()
    {
        var (client, _, notifier) = Pipeline(HttpStatusCode.Unauthorized);
        var expired = false;
        notifier.Expired += () => expired = true;

        await client.GetAsync(ApiRoutes.Dashboard.Mine);

        Assert.True(expired);
    }

    [Fact]
    public async Task Signing_in_does_not_send_a_stale_token()
    {
        // The auth endpoints are anonymous. Attaching a leftover credential would both leak it and
        // make a wrong password indistinguishable from an expired session.
        var (client, inner, _) = Pipeline(HttpStatusCode.OK);

        await client.PostAsync(ApiRoutes.Auth.Login, new StringContent("{}"));

        Assert.Null(inner.Seen!.Headers.Authorization);
    }

    [Fact]
    public async Task A_mistyped_password_does_not_sign_the_user_out()
    {
        // Regression: auth/login answers 401 for a wrong password. When the login POST still
        // carried a stale bearer token, that 401 looked exactly like an expired session, so the
        // seeker was signed out of the session they were trying to start and told "Your session
        // has ended" instead of "Invalid email/mobile or password".
        var (client, _, notifier) = Pipeline(HttpStatusCode.Unauthorized);
        var expired = false;
        notifier.Expired += () => expired = true;

        await client.PostAsync(ApiRoutes.Auth.Login, new StringContent("{}"));

        Assert.False(expired);
    }

    [Fact]
    public async Task Registering_does_not_send_a_stale_token_either()
    {
        var (client, inner, _) = Pipeline(HttpStatusCode.OK);

        await client.PostAsync(ApiRoutes.Auth.Register, new StringContent("{}"));

        Assert.Null(inner.Seen!.Headers.Authorization);
    }

    [Fact]
    public async Task A_401_with_no_stored_token_is_not_an_expiry()
    {
        // Nothing expired if nothing was ever presented.
        var (client, _, notifier) = Pipeline(HttpStatusCode.Unauthorized, new StubTokenStore { Token = null });
        var expired = false;
        notifier.Expired += () => expired = true;

        await client.GetAsync(ApiRoutes.Dashboard.Mine);

        Assert.False(expired);
    }

    [Fact]
    public async Task A_burst_of_401s_ends_the_session_once()
    {
        // One screen can have six requests in flight; each firing its own sign-out and navigation
        // would be six toasts and six redirects.
        var (client, _, notifier) = Pipeline(HttpStatusCode.Unauthorized);
        var count = 0;
        notifier.Expired += () => Interlocked.Increment(ref count);

        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => client.GetAsync(ApiRoutes.Dashboard.Mine)));

        Assert.Equal(1, count);
    }

    [Fact]
    public async Task Signing_in_again_re_arms_the_expiry_latch()
    {
        var (client, _, notifier) = Pipeline(HttpStatusCode.Unauthorized);
        var count = 0;
        notifier.Expired += () => count++;

        await client.GetAsync(ApiRoutes.Dashboard.Mine);
        notifier.Reset();                       // what AuthService.LoginAsync does
        await client.GetAsync(ApiRoutes.Dashboard.Mine);

        Assert.Equal(2, count);
    }

    [Fact]
    public async Task A_403_is_not_an_expired_session()
    {
        // Several endpoints answer Forbidden to a perfectly valid non-admin token.
        var (client, _, notifier) = Pipeline(HttpStatusCode.Forbidden);
        var expired = false;
        notifier.Expired += () => expired = true;

        await client.GetAsync(ApiRoutes.Dashboard.Admin);

        Assert.False(expired);
    }
}
