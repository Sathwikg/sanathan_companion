using App.Core.Services;
using Microsoft.AspNetCore.Components;

namespace App.Tests;

/// <summary>
/// Back on a phone: Android's button, the iPhone's edge swipe and the top bar's arrow all ask
/// <see cref="AppBackNavigator"/>. It used to be Android closing the app from any screen.
/// </summary>
public class BackNavigationTests
{
    private sealed class TestNav : NavigationManager
    {
        public string? LastTarget;
        public bool LastReplace;

        public TestNav(string path) => Initialize("https://0.0.0.0/", "https://0.0.0.0/" + path);

        protected override void NavigateToCore(string uri, NavigationOptions options)
        {
            LastTarget = uri;
            LastReplace = options.ReplaceHistoryEntry;
            Uri = ToAbsoluteUri(uri).ToString();
        }
    }

    private sealed class FakeWebView : INativeNavigation
    {
        public bool HasHistory;
        public string? Previous;
        public int WentBack;

        public bool IsNative => true;
        public bool CanGoBack() => HasHistory;
        public string? PreviousPath() => Previous;
        public void GoBack() => WentBack++;
        public void LeaveApp() { }
    }

    private static (AppBackNavigator Back, TestNav Nav, FakeWebView Web, OverlayStack Overlays) On(string path, bool history = false, string? previous = null)
    {
        var nav = new TestNav(path);
        var web = new FakeWebView { HasHistory = history, Previous = previous };
        var overlays = new OverlayStack();
        return (new AppBackNavigator(nav, overlays, web), nav, web, overlays);
    }

    [Fact]
    public async Task Back_closes_an_open_sheet_before_anything_else()
    {
        var (back, nav, web, overlays) = On("panchangam", history: true, previous: "sadhana");
        var closed = false;
        overlays.Push(() => { closed = true; return Task.CompletedTask; });

        Assert.True(await back.BackAsync());
        Assert.True(closed);
        Assert.Equal(0, web.WentBack);   // the page stays where it was
        Assert.Null(nav.LastTarget);
    }

    [Fact]
    public async Task The_overlay_on_top_closes_first_one_press_at_a_time()
    {
        var (back, _, _, overlays) = On("profile");
        var order = new List<string>();
        overlays.Push(() => { order.Add("sheet"); return Task.CompletedTask; });
        overlays.Push(() => { order.Add("confirm"); return Task.CompletedTask; });

        await back.BackAsync();
        Assert.Equal(["confirm"], order);

        await back.BackAsync();
        Assert.Equal(["confirm", "sheet"], order);
    }

    [Theory]
    [InlineData("")]
    [InlineData("mobile-dashboard")]
    public async Task On_the_home_screen_back_leaves_the_app(string home)
    {
        var (back, _, web, _) = On(home, history: true, previous: "sadhana");

        Assert.False(await back.BackAsync());   // MainPage then puts the app in the background
        Assert.Equal(0, web.WentBack);
    }

    [Fact]
    public async Task On_an_inner_page_back_returns_to_the_previous_page()
    {
        var (back, nav, web, _) = On("sadhana/chant/3f2a", history: true, previous: "sadhana");

        Assert.True(await back.BackAsync());
        Assert.Equal(1, web.WentBack);
        Assert.Null(nav.LastTarget);
    }

    [Theory]
    [InlineData("login")]
    [InlineData("login?returnUrl=%2Fsadhana")]
    [InlineData("register")]
    [InlineData("features")]
    public async Task Back_never_returns_to_the_sign_in_pages(string previous)
    {
        var (back, nav, web, _) = On("panchangam", history: true, previous: previous);

        Assert.True(await back.BackAsync());
        Assert.Equal(0, web.WentBack);
        Assert.Equal(string.Empty, nav.LastTarget);   // home instead
        Assert.True(nav.LastReplace);
    }

    [Fact]
    public async Task Opened_straight_onto_an_inner_page_back_goes_home_rather_than_closing()
    {
        // A tapped notification opens the app on its page with nothing behind it.
        var (back, nav, _, _) = On("calendar", history: false);

        Assert.True(await back.BackAsync());
        Assert.Equal(string.Empty, nav.LastTarget);
    }

    [Fact]
    public async Task On_the_sign_in_page_back_is_left_to_the_system()
    {
        var (back, _, _, _) = On("login");
        Assert.False(await back.BackAsync());
    }

    [Fact]
    public async Task A_registration_follows_its_overlay_open_and_closed_without_duplicates()
    {
        var overlays = new OverlayStack();
        var registration = new OverlayRegistration(overlays);
        Task Close() => Task.CompletedTask;

        registration.Sync(true, Close);
        registration.Sync(true, Close);   // re-rendered while still open
        Assert.Equal(1, overlays.Count);

        registration.Sync(false, Close);
        Assert.Equal(0, overlays.Count);

        registration.Sync(true, Close);
        registration.Dispose();            // torn down while open (navigated away)
        Assert.Equal(0, overlays.Count);
        Assert.False(await overlays.TryCloseTopAsync());
    }
}
