using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using App.Core.Auth;
using App.Core.Config;

namespace App.Tests;

/// <summary>
/// "Once signed in, stay signed in, like every other app on the phone." Only three things may end a
/// session: signing out, the server refusing the refresh token (signed out elsewhere, password
/// changed, account closed, a year unused), and losing the stored credentials (clear data,
/// reinstall). These pin the other cases, which used to sign seekers out.
/// </summary>
public class StaySignedInTests
{
    private sealed class Store : ITokenStore
    {
        public string? Token { get; set; }
        public string? Refresh { get; set; } = "stored-refresh";
        public bool Cleared { get; private set; }

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
            Cleared = true;
            return Task.CompletedTask;
        }
    }

    private sealed class NotGuest : IGuestModeStore
    {
        public Task<bool> IsGuestAsync() => Task.FromResult(false);
        public Task SetGuestAsync(bool isGuest) => Task.CompletedTask;
    }

    /// <summary>Plays the refresh endpoint: a fixed answer, a thrown network failure, or a hang.</summary>
    private sealed class RefreshEndpoint : HttpMessageHandler
    {
        private readonly Func<CancellationToken, Task<HttpResponseMessage>> _answer;
        public int Calls;
        public string? PlatformSent;

        private RefreshEndpoint(Func<CancellationToken, Task<HttpResponseMessage>> answer) => _answer = answer;

        public static RefreshEndpoint Answers(HttpStatusCode status, string body = "")
            => new(_ => Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) }));

        public static RefreshEndpoint Unreachable()
            => new(_ => throw new HttpRequestException("No such host is known."));

        public static RefreshEndpoint Hangs()
            => new(async ct => { await Task.Delay(Timeout.Infinite, ct); return new HttpResponseMessage(HttpStatusCode.OK); });

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref Calls);
            PlatformSent = request.Headers.TryGetValues("X-Platform", out var v) ? v.FirstOrDefault() : null;
            return _answer(cancellationToken);
        }
    }

    private sealed class Factory : IHttpClientFactory
    {
        private readonly HttpMessageHandler _handler;
        public Factory(HttpMessageHandler handler) => _handler = handler;
        public HttpClient CreateClient(string name) => new(_handler, disposeHandler: false) { BaseAddress = new Uri("https://example.test/api/") };
    }

    private static string Jwt(DateTime expires, string name = "Ravi")
        => new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            claims: new[] { new Claim("sub", Guid.NewGuid().ToString()), new Claim("name", name), new Claim(ClaimTypes.Role, "Sanathan") },
            expires: expires));

    private static string Renewed(string name = "Ravi")
        => "{\"token\":\"" + Jwt(DateTime.UtcNow.AddHours(2), name) + "\",\"refreshToken\":\"renewed-refresh\"}";

    private static async Task<ClaimsPrincipal> OpenAppAsync(Store store, RefreshEndpoint endpoint)
    {
        var provider = new JwtAuthenticationStateProvider(store, new NotGuest(), new TokenRefreshCoordinator(), new Factory(endpoint));
        return (await provider.GetAuthenticationStateAsync()).User;
    }

    // ================================================================== opening the app

    [Fact]
    public async Task Opening_the_app_hours_later_renews_the_session_instead_of_signing_out()
    {
        // The bug this replaces: an access token older than two hours made the app delete BOTH
        // tokens, so everyone who came back after lunch met the login page.
        var store = new Store { Token = Jwt(DateTime.UtcNow.AddHours(-5)) };
        var endpoint = RefreshEndpoint.Answers(HttpStatusCode.OK, Renewed("Lakshmi"));

        var user = await OpenAppAsync(store, endpoint);

        Assert.True(user.Identity?.IsAuthenticated);
        Assert.Equal("renewed-refresh", store.Refresh);
        Assert.Equal(1, endpoint.Calls);
        Assert.False(store.Cleared);
    }

    [Fact]
    public async Task Opening_the_app_with_no_signal_keeps_the_seeker_signed_in()
    {
        var store = new Store { Token = Jwt(DateTime.UtcNow.AddDays(-3)) };

        var user = await OpenAppAsync(store, RefreshEndpoint.Unreachable());

        Assert.True(user.Identity?.IsAuthenticated);
        Assert.False(store.Cleared);
        Assert.Equal("stored-refresh", store.Refresh);   // kept for when the signal returns
    }

    [Theory]
    [InlineData(HttpStatusCode.BadGateway)]           // the API restarting behind Caddy during a deploy
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.TooManyRequests)]      // the refresh rate limiter
    [InlineData(HttpStatusCode.RequestTimeout)]
    public async Task A_server_that_cannot_answer_right_now_does_not_sign_anyone_out(HttpStatusCode status)
    {
        var store = new Store { Token = Jwt(DateTime.UtcNow.AddHours(-3)) };

        var user = await OpenAppAsync(store, RefreshEndpoint.Answers(status, "<html>Bad gateway</html>"));

        Assert.True(user.Identity?.IsAuthenticated);
        Assert.False(store.Cleared);
    }

    [Fact]
    public async Task A_slow_network_does_not_hold_the_app_on_its_splash_screen()
    {
        var store = new Store { Token = Jwt(DateTime.UtcNow.AddHours(-3)) };
        var started = DateTime.UtcNow;

        var user = await OpenAppAsync(store, RefreshEndpoint.Hangs());

        Assert.True(user.Identity?.IsAuthenticated);
        Assert.True(DateTime.UtcNow - started < JwtAuthenticationStateProvider.StartupRefreshBudget + TimeSpan.FromSeconds(5));
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task Only_a_refusal_from_the_server_ends_the_session(HttpStatusCode status)
    {
        // Signed out on another device, password changed, account closed, or a year unused.
        var store = new Store { Token = Jwt(DateTime.UtcNow.AddHours(-3)) };

        var user = await OpenAppAsync(store, RefreshEndpoint.Answers(status));

        Assert.False(user.Identity?.IsAuthenticated);
        Assert.True(store.Cleared);
    }

    [Fact]
    public async Task A_fresh_token_needs_no_network_at_all()
    {
        var store = new Store { Token = Jwt(DateTime.UtcNow.AddHours(1)) };
        var endpoint = RefreshEndpoint.Unreachable();

        var user = await OpenAppAsync(store, endpoint);

        Assert.True(user.Identity?.IsAuthenticated);
        Assert.Equal(0, endpoint.Calls);
    }

    [Fact]
    public async Task A_lost_access_token_is_recovered_from_the_refresh_token()
    {
        // The two tokens are written one after the other; a process killed between the writes can
        // leave the refresh token alone. It is enough to carry on.
        var store = new Store { Token = null };

        var user = await OpenAppAsync(store, RefreshEndpoint.Answers(HttpStatusCode.OK, Renewed()));

        Assert.True(user.Identity?.IsAuthenticated);
    }

    [Fact]
    public async Task Without_any_stored_credential_the_app_shows_the_login_page()
    {
        // Clear data, a reinstall: nothing to renew from, and nothing is asked of the server.
        var store = new Store { Token = null, Refresh = null };
        var endpoint = RefreshEndpoint.Answers(HttpStatusCode.OK, Renewed());

        var user = await OpenAppAsync(store, endpoint);

        Assert.False(user.Identity?.IsAuthenticated);
        Assert.Equal(0, endpoint.Calls);
    }

    // ================================================================== while using the app

    private sealed class Api : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized));
    }

    private static (HttpClient Client, SessionExpiredNotifier Notifier) Pipeline(Store store, RefreshEndpoint endpoint)
    {
        var notifier = new SessionExpiredNotifier();
        var bearer = new BearerTokenHandler(store) { InnerHandler = new Api() };
        var expiry = new SessionExpiryHandler(store, notifier, new TokenRefreshCoordinator(), new Factory(endpoint)) { InnerHandler = bearer };
        return (new HttpClient(expiry) { BaseAddress = new Uri("https://example.test/api/") }, notifier);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    public async Task A_401_while_the_refresh_endpoint_is_down_fails_that_call_but_keeps_the_session(HttpStatusCode status)
    {
        var store = new Store { Token = "expired-jwt" };
        var (client, notifier) = Pipeline(store, RefreshEndpoint.Answers(status));
        var ended = false;
        notifier.Expired += () => ended = true;

        var response = await client.GetAsync(ApiRoutes.Dashboard.Mine);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.False(ended);
        Assert.Equal("stored-refresh", store.Refresh);
    }

    [Fact]
    public async Task A_401_with_no_signal_for_the_refresh_keeps_the_session()
    {
        var store = new Store { Token = "expired-jwt" };
        var (client, notifier) = Pipeline(store, RefreshEndpoint.Unreachable());
        var ended = false;
        notifier.Expired += () => ended = true;

        await client.GetAsync(ApiRoutes.Dashboard.Mine);

        Assert.False(ended);
    }

    [Fact]
    public async Task A_refused_refresh_still_ends_the_session()
    {
        var store = new Store { Token = "expired-jwt" };
        var (client, notifier) = Pipeline(store, RefreshEndpoint.Answers(HttpStatusCode.Unauthorized));
        var ended = false;
        notifier.Expired += () => ended = true;

        await client.GetAsync(ApiRoutes.Dashboard.Mine);

        Assert.True(ended);
    }
}
