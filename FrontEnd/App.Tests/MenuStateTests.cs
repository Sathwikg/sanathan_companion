using System.Reflection;
using App.Core.Auth;
using App.Core.Config;
using App.Core.DependencyInjection;
using App.Core.Models;
using App.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace App.Tests;

/// <summary>
/// The phone's layout and home page share one menu, and that menu is also the phone's access
/// list — so it must be fetched once, survive an unreachable API, and never show one user or one
/// language the other's.
/// </summary>
public class MenuStateTests
{
    /// <summary>
    /// IApiClient has over a hundred members and MenuState needs one. Every other call fails the
    /// way an unreachable server does, which LocalizationState already survives by falling back
    /// to English — so the real LocalizationState can be used rather than a stand-in.
    /// </summary>
    public class FakeApi : DispatchProxy
    {
        /// <summary>Replies to hand out in order; once drained, every fetch succeeds.</summary>
        public Queue<Task<List<MenuTreeNode>>> Replies { get; } = new();

        /// <summary>The X-App-Language each menu request would have carried.</summary>
        public List<string?> MenuLanguages { get; } = new();

        public LanguageContext? Language { get; set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name != nameof(IApiClient.GetMenuAsync))
                throw new HttpRequestException("offline");

            MenuLanguages.Add(Language?.Code);
            return Replies.Count > 0 ? Replies.Dequeue() : Task.FromResult(Menu("/sadhana"));
        }
    }

    private sealed class StubLanguageStore(string? code) : ILanguageStore
    {
        public Task<string?> GetLanguageAsync() => Task.FromResult(code);

        public Task SetLanguageAsync(string languageCode)
        {
            code = languageCode;
            return Task.CompletedTask;
        }
    }

    private static (MenuState Menus, FakeApi Api, LocalizationState Loc) Create(string? storedLanguage = "en")
    {
        var api = DispatchProxy.Create<IApiClient, FakeApi>();
        var fake = (FakeApi)(object)api;
        var language = new LanguageContext();
        fake.Language = language;

        var loc = new LocalizationState(api, new StubLanguageStore(storedLanguage), language);
        return (new MenuState(api, loc, language), fake, loc);
    }

    private static List<MenuTreeNode> Menu(params string[] routes)
        => routes.Select(r => new MenuTreeNode { Id = Guid.NewGuid(), Name = r, RoutePath = r }).ToList();

    private static TaskCompletionSource<List<MenuTreeNode>> Pending()
        => new(TaskCreationOptions.RunContinuationsAsynchronously);

    [Fact]
    public async Task The_layout_and_the_page_share_one_fetch()
    {
        var (menus, api, _) = Create();
        var reply = Pending();
        api.Replies.Enqueue(reply.Task);

        var layout = menus.EnsureLoadedAsync();
        var page = menus.EnsureLoadedAsync();
        reply.SetResult(Menu("/sadhana"));
        await Task.WhenAll(layout, page);
        await menus.EnsureLoadedAsync();

        Assert.Single(api.MenuLanguages);
        Assert.True(menus.CanOpen("sadhana"));
    }

    [Fact]
    public async Task An_unreachable_api_yields_an_empty_menu_not_an_exception()
    {
        var (menus, api, _) = Create();
        api.Replies.Enqueue(Task.FromException<List<MenuTreeNode>>(new HttpRequestException("offline")));

        await menus.EnsureLoadedAsync();

        Assert.NotNull(menus.Menu);
        Assert.Empty(menus.Menu);
        Assert.False(menus.CanOpen("sadhana"));
    }

    /// <summary>
    /// The phone home gates its tiles and widgets on this menu, so a failure must not be cached as
    /// the answer: a request that timed out on a cold API would strip the home screen until the
    /// next sign-in. The next caller (the seeker returning to the home page) asks again.
    /// </summary>
    [Fact]
    public async Task A_failed_fetch_is_retried_by_the_next_caller()
    {
        var (menus, api, _) = Create();
        api.Replies.Enqueue(Task.FromException<List<MenuTreeNode>>(new HttpRequestException("cold start")));

        await menus.EnsureLoadedAsync();
        Assert.False(menus.CanOpen("sadhana"));

        await menus.EnsureLoadedAsync();
        await menus.EnsureLoadedAsync();

        Assert.True(menus.CanOpen("sadhana"));
        Assert.Equal(2, api.MenuLanguages.Count);   // retried once, then cached like any success
    }

    /// <summary>
    /// Sign-in resets every IUserSessionState, and that only clears the menu components see if the
    /// alias resolves to the same scoped instance. A plain AddScoped&lt;IUserSessionState, MenuState&gt;
    /// would build a second MenuState, reset that one, and leave the previous user's quick actions
    /// on screen.
    /// </summary>
    [Fact]
    public void The_session_reset_reaches_the_instance_components_inject()
    {
        var services = new ServiceCollection();
        services.AddAppCore(new AppConfig());
        services.AddScoped(_ => DispatchProxy.Create<IApiClient, FakeApi>());
        services.AddScoped<ILanguageStore>(_ => new StubLanguageStore("en"));

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var menus = scope.ServiceProvider.GetRequiredService<MenuState>();

        Assert.Contains(scope.ServiceProvider.GetServices<IUserSessionState>(), s => ReferenceEquals(s, menus));
    }

    [Fact]
    public async Task CanOpen_is_false_until_loaded_and_ignores_the_leading_slash()
    {
        var (menus, _, _) = Create();
        Assert.False(menus.CanOpen("sadhana"));

        await menus.EnsureLoadedAsync();

        Assert.True(menus.CanOpen("sadhana"));
        Assert.True(menus.CanOpen("/sadhana"));
        Assert.True(menus.CanOpen("sadhana/chant/8f0d2c1e-5b7a-4c1e-9d3f-2a6b8e4f1c07"));
        Assert.False(menus.CanOpen("panchangam"));
    }

    [Fact]
    public async Task Reset_forgets_the_menu_announces_it_and_the_next_user_fetches_their_own()
    {
        var (menus, api, _) = Create();
        await menus.EnsureLoadedAsync();
        var raised = 0;
        menus.OnChanged += () => raised++;

        menus.Reset();

        Assert.Null(menus.Menu);
        Assert.Equal(1, raised);

        await menus.EnsureLoadedAsync();

        Assert.Equal(2, api.MenuLanguages.Count);
        Assert.Equal(2, raised);
    }

    [Fact]
    public async Task A_reply_for_the_previous_user_is_dropped()
    {
        var (menus, api, _) = Create();
        var stale = Pending();
        api.Replies.Enqueue(stale.Task);

        var load = menus.EnsureLoadedAsync();
        menus.Reset();
        stale.SetResult(Menu("/sadhana"));
        await load;

        Assert.Null(menus.Menu);
    }

    [Fact]
    public async Task A_reload_supersedes_a_slower_load_and_its_callers_wait_for_it()
    {
        var (menus, api, _) = Create();
        var slow = Pending();
        var fresh = Pending();
        api.Replies.Enqueue(slow.Task);
        api.Replies.Enqueue(fresh.Task);

        var first = menus.EnsureLoadedAsync();
        var reload = menus.ReloadAsync();

        // The superseded reply lands first: it must neither write nor release the caller that
        // joined it, who would otherwise read a menu that has not arrived.
        slow.SetResult(Menu("/sadhana"));
        Assert.NotSame(first, await Task.WhenAny(first, Task.Delay(200)));
        Assert.Null(menus.Menu);

        fresh.SetResult(Menu("/festivals"));
        await Task.WhenAll(first, reload);

        Assert.True(menus.CanOpen("festivals"));
        Assert.False(menus.CanOpen("sadhana"));
    }

    [Fact]
    public async Task The_menu_is_fetched_in_the_chosen_language_and_again_after_a_switch()
    {
        // No locale list and no bundle reachable: the UI falls back to English, but the header —
        // and so the server's menu names — still follow the stored choice.
        var (menus, api, loc) = Create(storedLanguage: "te");

        await menus.EnsureLoadedAsync();
        Assert.Equal(["te"], api.MenuLanguages);

        await loc.SelectAsync("ta");
        await menus.EnsureLoadedAsync();
        await menus.EnsureLoadedAsync();

        Assert.Equal(["te", "ta"], api.MenuLanguages);
    }
}
