using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using App.Core.Auth;
using App.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace App.Tests;

/// <summary>
/// "Continue as Guest": who counts as a guest, which pages let them in, and how signing in brings
/// them back to where they were.
/// </summary>
public class GuestModeTests
{
    // ================================================================== the return address

    [Theory]
    [InlineData("sadhana/chant/0f8fad5b-d9cb-469f-a165-70867728950e", "sadhana/chant/0f8fad5b-d9cb-469f-a165-70867728950e")]
    [InlineData("/favorites", "favorites")]
    [InlineData("panchangam?date=2026-10-22", "panchangam?date=2026-10-22")]
    [InlineData("  wallpapers-download  ", "wallpapers-download")]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    public void An_in_app_path_survives(string? given, string expected)
        => Assert.Equal(expected, LoginGate.SafeReturnUrl(given));

    [Theory]
    [InlineData("https://evil.example/phish")]
    [InlineData("http:evil.example")]
    [InlineData("//evil.example/phish")]
    [InlineData("/\\evil.example")]
    [InlineData("\\\\evil.example")]
    [InlineData("javascript:alert(1)")]
    [InlineData("JavaScript:alert(1)")]
    [InlineData("sadhana\r\nSet-Cookie: x")]
    public void Anything_that_leaves_the_app_goes_home(string given)
        => Assert.Equal(string.Empty, LoginGate.SafeReturnUrl(given));

    [Theory]
    [InlineData("login")]
    [InlineData("/login?returnUrl=sadhana")]
    [InlineData("register")]
    [InlineData("Register/")]
    public void The_auth_pages_are_never_a_destination(string given)
        => Assert.Equal(string.Empty, LoginGate.SafeReturnUrl(given));

    [Fact]
    public void A_colon_after_the_path_starts_is_not_a_scheme()
        => Assert.Equal("panchangam?at=10:30", LoginGate.SafeReturnUrl("panchangam?at=10:30"));

    [Fact]
    public void The_login_url_carries_the_return_address_encoded()
    {
        Assert.Equal("login?returnUrl=sadhana%2Fchant%2Fabc", LoginGate.LoginUrl("sadhana/chant/abc"));
        Assert.Equal("register?returnUrl=favorites", LoginGate.RegisterUrl("favorites"));

        // Home needs no return address, and an unsafe one is dropped rather than carried.
        Assert.Equal("login", LoginGate.LoginUrl(""));
        Assert.Equal("login", LoginGate.LoginUrl("https://evil.example"));
    }

    // ================================================================== who is a guest

    private sealed class StubTokenStore : ITokenStore
    {
        public string? Token { get; set; }
        public Task<string?> GetTokenAsync() => Task.FromResult(Token);
        public Task<string?> GetRefreshTokenAsync() => Task.FromResult<string?>(null);
        public Task SetTokensAsync(string accessToken, string refreshToken) { Token = accessToken; return Task.CompletedTask; }
        public Task ClearTokenAsync() { Token = null; return Task.CompletedTask; }
    }

    private sealed class StubGuestStore : IGuestModeStore
    {
        public bool IsGuest { get; set; }
        public Task<bool> IsGuestAsync() => Task.FromResult(IsGuest);
        public Task SetGuestAsync(bool isGuest) { IsGuest = isGuest; return Task.CompletedTask; }
    }

    private static string Jwt(DateTime expires) => new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
        claims: new[] { new Claim("sub", Guid.NewGuid().ToString()), new Claim(ClaimTypes.Role, "Sanathan") },
        expires: expires));

    private static async Task<ClaimsPrincipal> UserAsync(string? token, bool guest)
    {
        var provider = new JwtAuthenticationStateProvider(new StubTokenStore { Token = token }, new StubGuestStore { IsGuest = guest });
        return (await provider.GetAuthenticationStateAsync()).User;
    }

    [Fact]
    public async Task A_visitor_who_chose_guest_is_a_guest_and_still_not_signed_in()
    {
        var user = await UserAsync(token: null, guest: true);

        Assert.True(GuestSession.IsGuest(user));
        Assert.False(GuestSession.IsMember(user));
        // Unauthenticated on purpose: every plain [Authorize] page stays closed to them.
        Assert.False(user.Identity?.IsAuthenticated);
    }

    [Fact]
    public async Task A_visitor_who_has_not_chosen_is_neither()
    {
        var user = await UserAsync(token: null, guest: false);

        Assert.False(GuestSession.IsGuest(user));
        Assert.False(GuestSession.IsMember(user));
    }

    [Fact]
    public async Task A_valid_token_wins_over_a_stale_guest_flag()
    {
        var user = await UserAsync(Jwt(DateTime.UtcNow.AddHours(1)), guest: true);

        Assert.True(GuestSession.IsMember(user));
        Assert.False(GuestSession.IsGuest(user));
    }

    [Fact]
    public async Task An_expired_session_is_not_quietly_turned_into_a_guest()
    {
        // Signing in clears the flag, so an expired seeker lands on the login page.
        var user = await UserAsync(Jwt(DateTime.UtcNow.AddMinutes(-5)), guest: false);

        Assert.False(GuestSession.IsMember(user));
        Assert.False(GuestSession.IsGuest(user));
    }

    // ================================================================== which pages let them in

    private static IAuthorizationService Authorization()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorizationCore(GuestSession.AddPolicies);
        return services.BuildServiceProvider().GetRequiredService<IAuthorizationService>();
    }

    [Fact]
    public async Task A_guest_may_browse_but_not_open_a_members_page()
    {
        var auth = Authorization();
        var guest = GuestSession.CreatePrincipal();

        Assert.True((await auth.AuthorizeAsync(guest, null, GuestSession.BrowsePolicy)).Succeeded);

        // What [Authorize] with no policy asks for: the japa screen, Favorites, Profile…
        var membersOnly = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
        Assert.False((await auth.AuthorizeAsync(guest, null, membersOnly)).Succeeded);
    }

    [Fact]
    public async Task A_seeker_may_browse_and_a_stranger_may_not()
    {
        var auth = Authorization();
        var seeker = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("sub", "x") }, "jwt"));
        var stranger = new ClaimsPrincipal(new ClaimsIdentity());

        Assert.True((await auth.AuthorizeAsync(seeker, null, GuestSession.BrowsePolicy)).Succeeded);
        Assert.False((await auth.AuthorizeAsync(stranger, null, GuestSession.BrowsePolicy)).Succeeded);
    }

    // ================================================================== the gate on an action

    private sealed class RecordingNavigation : NavigationManager
    {
        public RecordingNavigation(string uri) => Initialize("https://app.example/", uri);

        public string? NavigatedTo { get; private set; }

        protected override void NavigateToCore(string uri, NavigationOptions options) => NavigatedTo = uri;
    }

    private sealed class FixedAuthState : Microsoft.AspNetCore.Components.Authorization.AuthenticationStateProvider
    {
        private readonly ClaimsPrincipal _user;
        public FixedAuthState(ClaimsPrincipal user) => _user = user;
        public override Task<Microsoft.AspNetCore.Components.Authorization.AuthenticationState> GetAuthenticationStateAsync()
            => Task.FromResult(new Microsoft.AspNetCore.Components.Authorization.AuthenticationState(_user));
    }

    [Fact]
    public async Task A_guest_reaching_for_a_favourite_is_sent_to_sign_in_and_back()
    {
        var nav = new RecordingNavigation("https://app.example/sadhana");
        var gate = new LoginGate(nav, new FixedAuthState(GuestSession.CreatePrincipal()));

        var allowed = await gate.EnsureSignedInAsync();

        Assert.False(allowed);
        Assert.Equal("login?returnUrl=sadhana", nav.NavigatedTo);
    }

    [Fact]
    public async Task A_seeker_is_let_through_without_a_detour()
    {
        var nav = new RecordingNavigation("https://app.example/sadhana");
        var seeker = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("sub", "x") }, "jwt"));
        var gate = new LoginGate(nav, new FixedAuthState(seeker));

        Assert.True(await gate.EnsureSignedInAsync());
        Assert.Null(nav.NavigatedTo);
    }
}
