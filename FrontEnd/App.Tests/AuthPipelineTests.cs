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

        /// <summary>Null by default, so a 401 ends the session without attempting a refresh.</summary>
        public string? Refresh { get; set; }

        public Task<string?> GetTokenAsync() => Task.FromResult(Token);
        public Task<string?> GetRefreshTokenAsync() => Task.FromResult(Refresh);

        public Task SetTokensAsync(string accessToken, string refreshToken)
        {
            Token = accessToken;
            Refresh = refreshToken;
            return Task.CompletedTask;
        }

        public Task ClearTokenAsync()
        {
            Token = null;
            Refresh = null;
            return Task.CompletedTask;
        }
    }

    /// <summary>Stands in for IHttpClientFactory, which SessionExpiryHandler uses for the refresh call.</summary>
    private sealed class StubClientFactory : IHttpClientFactory
    {
        private readonly HttpMessageHandler _handler;
        public StubClientFactory(HttpMessageHandler handler) => _handler = handler;

        public HttpClient CreateClient(string name)
            => new(_handler, disposeHandler: false) { BaseAddress = new Uri("https://example.test/api/") };
    }

    /// <summary>Answers every refresh attempt with the given status and body.</summary>
    private sealed class RefreshHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly string _body;
        private int _calls;

        public RefreshHandler(HttpStatusCode status, string body = "")
        {
            _status = status;
            _body = body;
        }

        /// <summary>Volatile read: the increments come from whichever thread lost the race.</summary>
        public int Calls => Volatile.Read(ref _calls);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _calls);
            return Task.FromResult(new HttpResponseMessage(_status) { Content = new StringContent(_body) });
        }
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
        HttpStatusCode status, StubTokenStore? store = null, HttpMessageHandler? refreshWith = null)
    {
        store ??= new StubTokenStore();
        var notifier = new SessionExpiredNotifier();
        var inner = new CapturingHandler(status);

        // Mirrors AddAppCore: the expiry handler is OUTERMOST, wrapping the bearer handler, so it
        // observes the Authorization header the inner handler added.
        var bearer = new BearerTokenHandler(store) { InnerHandler = inner };
        var expiry = new SessionExpiryHandler(
            store,
            notifier,
            new TokenRefreshCoordinator(),
            new StubClientFactory(refreshWith ?? new RefreshHandler(HttpStatusCode.Unauthorized)))
        {
            InnerHandler = bearer
        };

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

    // ---------------------------------------------------------------- renewal

    /// <summary>Two-stage inner handler: 401 first, then whatever comes next.</summary>
    private sealed class ThenHandler : HttpMessageHandler
    {
        private readonly Queue<HttpStatusCode> _statuses;
        private readonly object _gate = new();
        private readonly List<string?> _presented = new();

        public ThenHandler(params HttpStatusCode[] statuses) => _statuses = new Queue<HttpStatusCode>(statuses);

        /// <summary>Snapshot, because the burst test reads this while requests are still landing.</summary>
        public List<string?> Presented
        {
            get { lock (_gate) return new List<string?>(_presented); }
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            HttpStatusCode status;
            lock (_gate)
            {
                _presented.Add(request.Headers.Authorization?.Parameter);
                status = _statuses.Count > 0 ? _statuses.Dequeue() : HttpStatusCode.OK;
            }

            return Task.FromResult(new HttpResponseMessage(status));
        }
    }

    private const string RenewedPair =
        "{\"token\":\"renewed-jwt\",\"refreshToken\":\"renewed-refresh\"}";

    private static (HttpClient Client, ThenHandler Inner, SessionExpiredNotifier Notifier, StubTokenStore Store) RenewablePipeline(
        HttpMessageHandler refreshWith, params HttpStatusCode[] statuses)
    {
        var store = new StubTokenStore { Refresh = "stored-refresh" };
        var notifier = new SessionExpiredNotifier();
        var inner = new ThenHandler(statuses);

        var bearer = new BearerTokenHandler(store) { InnerHandler = inner };
        var expiry = new SessionExpiryHandler(store, notifier, new TokenRefreshCoordinator(), new StubClientFactory(refreshWith))
        {
            InnerHandler = bearer
        };

        return (new HttpClient(expiry) { BaseAddress = new Uri("https://example.test/api/") }, inner, notifier, store);
    }

    [Fact]
    public async Task An_expired_token_is_renewed_and_the_request_replayed()
    {
        // The whole point: a seeker coming back to the app after two hours should not be signed
        // out, they should simply carry on.
        var (client, inner, notifier, store) = RenewablePipeline(
            new RefreshHandler(HttpStatusCode.OK, RenewedPair),
            HttpStatusCode.Unauthorized, HttpStatusCode.OK);

        var expired = false;
        notifier.Expired += () => expired = true;

        var response = await client.GetAsync(ApiRoutes.Dashboard.Mine);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(expired);
        Assert.Equal(new[] { "stored-jwt", "renewed-jwt" }, inner.Presented);
        Assert.Equal("renewed-jwt", store.Token);
        Assert.Equal("renewed-refresh", store.Refresh);
    }

    [Fact]
    public async Task A_refresh_that_is_itself_refused_ends_the_session()
    {
        var (client, _, notifier, _) = RenewablePipeline(
            new RefreshHandler(HttpStatusCode.Unauthorized),
            HttpStatusCode.Unauthorized);

        var expired = false;
        notifier.Expired += () => expired = true;

        await client.GetAsync(ApiRoutes.Dashboard.Mine);

        Assert.True(expired);
    }

    [Fact]
    public async Task A_replay_that_is_still_refused_ends_the_session_rather_than_returning_a_bare_401()
    {
        // The account was closed between the refresh and the replay. Handing the 401 back to the
        // caller is exactly the failure this handler exists to prevent: a screen that shows
        // "Response status code does not indicate success: 401" and a loader that never stops.
        var (client, _, notifier, _) = RenewablePipeline(
            new RefreshHandler(HttpStatusCode.OK, RenewedPair),
            HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized);

        var expired = false;
        notifier.Expired += () => expired = true;

        await client.GetAsync(ApiRoutes.Dashboard.Mine);

        Assert.True(expired);
    }

    /// <summary>Refuses every token but one, the way a real API does.</summary>
    private sealed class TokenAwareHandler : HttpMessageHandler
    {
        private readonly string _accepted;
        public TokenAwareHandler(string accepted) => _accepted = accepted;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(
                request.Headers.Authorization?.Parameter == _accepted
                    ? HttpStatusCode.OK
                    : HttpStatusCode.Unauthorized));
    }

    [Fact]
    public async Task A_burst_of_expired_requests_refreshes_once()
    {
        // Presenting the same refresh token twice is the signature the server revokes a whole
        // token family on, so a dashboard firing six requests at once must not race.
        var store = new StubTokenStore { Refresh = "stored-refresh" };
        var refresh = new RefreshHandler(HttpStatusCode.OK, RenewedPair);

        var bearer = new BearerTokenHandler(store) { InnerHandler = new TokenAwareHandler("renewed-jwt") };
        var expiry = new SessionExpiryHandler(
            store, new SessionExpiredNotifier(), new TokenRefreshCoordinator(), new StubClientFactory(refresh))
        {
            InnerHandler = bearer
        };

        using var client = new HttpClient(expiry) { BaseAddress = new Uri("https://example.test/api/") };
        var responses = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => client.GetAsync(ApiRoutes.Dashboard.Mine)));

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        Assert.Equal(1, refresh.Calls);
    }

    [Fact]
    public async Task Refreshing_does_not_carry_the_dead_access_token()
    {
        var (client, inner, _, _) = RenewablePipeline(
            new RefreshHandler(HttpStatusCode.OK, RenewedPair),
            HttpStatusCode.Unauthorized, HttpStatusCode.OK);

        await client.PostAsync(ApiRoutes.Auth.Refresh, new StringContent("{}"));

        Assert.Null(inner.Presented[0]);
    }
}
