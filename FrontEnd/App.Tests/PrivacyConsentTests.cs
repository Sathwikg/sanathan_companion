using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using App.Core.Auth;
using App.Core.Config;
using App.Core.Models;
using App.Core.Services;
using Microsoft.AspNetCore.Components;

namespace App.Tests;

/// <summary>
/// The app's side of the Digital Personal Data Protection Act, 2023: sign-up carries the consent
/// the server demands, the notice version agrees with the server's, and page visits (an optional
/// purpose) are sent only for a seeker who switched usage statistics on.
/// </summary>
public class PrivacyConsentTests
{
    // ------------------------------------------------------------------ fakes

    /// <summary>Answers GET profile/privacy with a settings body (or a status) and records every call.</summary>
    private sealed class PrivacyServer : HttpMessageHandler
    {
        public bool UsageAnalytics { get; set; }
        public HttpStatusCode PrivacyStatus { get; set; } = HttpStatusCode.OK;
        public List<string> Calls { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath.Trim('/');
            lock (Calls) Calls.Add($"{request.Method} {path}");

            if (path.EndsWith(ApiRoutes.Profile.Privacy, StringComparison.Ordinal))
            {
                if (PrivacyStatus != HttpStatusCode.OK)
                    return Task.FromResult(new HttpResponseMessage(PrivacyStatus));

                var body = JsonSerializer.Serialize(new PrivacySettings
                {
                    AcceptedNoticeVersion = PrivacyNoticeInfo.CurrentVersion,
                    CurrentNoticeVersion = PrivacyNoticeInfo.CurrentVersion,
                    UsageAnalytics = UsageAnalytics
                });
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(body, Encoding.UTF8, "application/json")
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
        }

        public int Count(string fragment)
        {
            lock (Calls) return Calls.Count(c => c.Contains(fragment, StringComparison.Ordinal));
        }
    }

    private sealed class SignedInTokens : ITokenStore
    {
        public string? Token { get; set; } = "jwt";
        public Task<string?> GetTokenAsync() => Task.FromResult(Token);
        public Task<string?> GetRefreshTokenAsync() => Task.FromResult<string?>(null);
        public Task SetTokensAsync(string accessToken, string refreshToken) => Task.CompletedTask;
        public Task ClearTokenAsync() => Task.CompletedTask;
    }

    private sealed class SwitchableLifecycle : IAppLifecycle
    {
        public bool IsActive { get; private set; } = true;
        public event Action? Changed;

        public void Background()
        {
            IsActive = false;
            Changed?.Invoke();
        }
    }

    private sealed class TestNavigation : NavigationManager
    {
        public TestNavigation() => Initialize("https://app.test/", "https://app.test/calendar");
        protected override void NavigateToCore(string uri, bool forceLoad) => Uri = ToAbsoluteUri(uri).ToString();
    }

    private static (ApiClient Api, PrivacyServer Server) Client()
    {
        var server = new PrivacyServer();
        var http = new HttpClient(server) { BaseAddress = new Uri("https://api.test/api/") };
        return (new ApiClient(http, new AppConfig()), server);
    }

    // ------------------------------------------------------------------ sign-up

    [Fact]
    public void Sign_up_starts_unticked_and_names_the_notice_the_app_shows()
    {
        // A pre-ticked box is not consent: both must be ticked by the seeker.
        var plain = new RegisterRequest();
        var google = new GoogleRegisterRequest();

        Assert.False(plain.ConfirmAdult || plain.AcceptPrivacyNotice);
        Assert.False(google.ConfirmAdult || google.AcceptPrivacyNotice);
        Assert.Equal(PrivacyNoticeInfo.CurrentVersion, plain.PrivacyNoticeVersion);
        Assert.Equal(PrivacyNoticeInfo.CurrentVersion, google.PrivacyNoticeVersion);
    }

    [Fact]
    public void The_consent_reaches_the_server_under_the_names_it_reads()
    {
        var json = JsonSerializer.Serialize(new RegisterRequest { ConfirmAdult = true, AcceptPrivacyNotice = true },
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Contains("\"confirmAdult\":true", json);
        Assert.Contains("\"acceptPrivacyNotice\":true", json);
        Assert.Contains($"\"privacyNoticeVersion\":{PrivacyNoticeInfo.CurrentVersion}", json);
    }

    [Fact]
    public void The_app_and_the_server_agree_on_the_notice_version()
    {
        // The server refuses consent to any other version, so a mismatch would block every sign-up.
        var root = FindRepoRoot();
        var server = File.ReadAllText(Path.Combine(root.FullName,
            "BackEnd", "src", "Sanathana.Companion.Application", "Common", "DataProtection.cs"));

        var version = Regex.Match(server, @"CurrentVersion\s*=\s*(\d+)");
        var date = Regex.Match(server, @"EffectiveDate\s*=\s*new\((\d+),\s*(\d+),\s*(\d+)\)");

        Assert.True(version.Success && date.Success, "PrivacyNotice constants not found in DataProtection.cs.");
        Assert.Equal(int.Parse(version.Groups[1].Value), PrivacyNoticeInfo.CurrentVersion);
        Assert.Equal(new DateOnly(int.Parse(date.Groups[1].Value), int.Parse(date.Groups[2].Value), int.Parse(date.Groups[3].Value)),
            PrivacyNoticeInfo.EffectiveDate);
    }

    [Fact]
    public void The_notice_names_a_real_postal_address_and_no_placeholder_is_left()
    {
        // DPDP Rules, rule 3: the seeker must be able to reach the Data Fiduciary.
        Assert.False(string.IsNullOrWhiteSpace(Brand.PostalAddress), "BrandPostalAddress is empty in Branding.props.");
        Assert.Matches(@"\b\d{6}\b", Brand.PostalAddress);   // an Indian PIN code
        // DPDP Act, section 8(10): a named Grievance Officer, not the bare title.
        Assert.False(string.IsNullOrWhiteSpace(Brand.GrievanceOfficer), "BrandGrievanceOfficer is empty in Branding.props.");
        Assert.DoesNotContain("Grievance Officer", Brand.GrievanceOfficer);

        var root = FindRepoRoot();
        foreach (var file in new[]
                 {
                     Path.Combine("FrontEnd", "App.UI.Shared", "Components", "PrivacyNoticeBody.razor"),
                     Path.Combine("FrontEnd", "App.Web", "privacy.template.html"),
                     Path.Combine("FrontEnd", "App.UI.Shared", "privacy.template.html"),
                 })
        {
            var text = File.ReadAllText(Path.Combine(root.FullName, file));
            Assert.DoesNotContain("placeholder-tag\">[", text);
            Assert.Contains("PostalAddress", text);
            Assert.Contains("GrievanceOfficer", text);
        }
    }

    // ------------------------------------------------------------------ privacy state

    [Fact]
    public async Task The_settings_are_fetched_once_and_again_after_a_sign_in()
    {
        var (api, server) = Client();
        var state = new PrivacyState(api);

        await Task.WhenAll(state.LoadAsync(), state.LoadAsync());
        await state.LoadAsync();
        Assert.Equal(1, server.Count(ApiRoutes.Profile.Privacy));

        state.Reset();   // what AuthService does on sign-in and sign-out
        Assert.Null(state.Current);
        await state.LoadAsync();
        Assert.Equal(2, server.Count(ApiRoutes.Profile.Privacy));
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.NotFound)]   // a server not yet deployed with the endpoint
    public async Task An_unanswered_fetch_asks_nothing_and_allows_nothing(HttpStatusCode status)
    {
        var (api, server) = Client();
        server.PrivacyStatus = status;
        var state = new PrivacyState(api);

        Assert.Null(await state.LoadAsync());
        Assert.False(state.NoticeUpdateRequired);
        Assert.False(state.AllowsUsageAnalytics);
    }

    // ------------------------------------------------------------------ page visits

    [Theory]
    [InlineData(false, 0)]
    [InlineData(true, 1)]
    public async Task Page_visits_are_sent_only_with_consent(bool consented, int expectedVisits)
    {
        var (api, server) = Client();
        server.UsageAnalytics = consented;
        var lifecycle = new SwitchableLifecycle();
        using var tracker = new NavigationTrackerService(new TestNavigation(), api, new SignedInTokens(), lifecycle, new PrivacyState(api));
        tracker.Initialize();

        await Task.Delay(1100);   // a visit under a second is a bounce, not a visit
        lifecycle.Background();   // closes the visit and sends it
        await WaitForAsync(() => server.Count(ApiRoutes.Profile.Privacy) > 0);
        await Task.Delay(200);

        Assert.Equal(expectedVisits, server.Calls.Count(c => c.StartsWith("POST", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task A_guest_is_never_tracked_and_never_asked()
    {
        var (api, server) = Client();
        server.UsageAnalytics = true;
        var lifecycle = new SwitchableLifecycle();
        using var tracker = new NavigationTrackerService(new TestNavigation(), api, new SignedInTokens { Token = null }, lifecycle, new PrivacyState(api));
        tracker.Initialize();

        await Task.Delay(1100);
        lifecycle.Background();
        await Task.Delay(300);

        Assert.Empty(server.Calls);
    }

    // ------------------------------------------------------------------ helpers

    private static async Task WaitForAsync(Func<bool> condition)
    {
        for (var i = 0; i < 50 && !condition(); i++) await Task.Delay(20);
    }

    private static DirectoryInfo FindRepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "BackEnd")) && Directory.Exists(Path.Combine(dir.FullName, "FrontEnd")))
                return dir;
        }
        throw new DirectoryNotFoundException("The repository root (BackEnd + FrontEnd) was not found above the test binaries.");
    }
}
