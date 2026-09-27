using App.Core.Models;

namespace App.Core.Services;

/// <summary>
/// The phone's menu, fetched once and shared: the shell builds its bottom bar and "More" sheet
/// from it, and pages ask it whether a shortcut would open or land on the access-denied page.
/// </summary>
/// <remarks>
/// Shared rather than fetched per component because the layout and the page it hosts need the
/// same answer at the same moment. Two fetches would cost two round trips, and could disagree for
/// a render if an administrator changed Access Rights in between — a quick action whose tab has
/// just vanished from the bar.
/// <para>
/// Modelled on <see cref="RegionState"/>: an idempotent load, an <see cref="OnChanged"/> event
/// and a generation counter so a response started for someone else never lands.
/// </para>
/// </remarks>
public class MenuState : IUserSessionState
{
    private readonly IApiClient _api;
    private readonly LocalizationState _loc;
    private readonly LanguageContext _language;
    private Task? _loadTask;

    /// <summary>The language the cached load was requested in. Menu names are translated
    /// server-side, so a menu from another language is stale even though its routes are not.</summary>
    private string? _loadedCode;

    /// <summary>Bumped by every load and every <see cref="Reset"/>, so only the newest request may
    /// write — a slow reply must not overwrite a reload, a language switch or the next user.</summary>
    private int _generation;

    /// <summary>The cached load ended in the empty "unreachable" menu. Kept apart from the menu
    /// itself because the phone home gates its tiles and widgets on it: a menu request that timed
    /// out while the API was still waking up would otherwise strip the home screen for the rest of
    /// the session, since nothing short of a sign-in, a language switch or a module edit refetches.</summary>
    private bool _failed;

    public MenuState(IApiClient api, LocalizationState loc, LanguageContext language)
    {
        _api = api;
        _loc = loc;
        _language = language;
    }

    /// <summary>
    /// The menu the server returned. Null until the first load completes; empty when the API could
    /// not be reached.
    /// </summary>
    public List<MenuTreeNode>? Menu { get; private set; }

    /// <summary>Raised after <see cref="Menu"/> changes: a load, a reload or a reset.</summary>
    public event Action? OnChanged;

    /// <summary>
    /// Loads the menu once; safe to call from many components concurrently. Fetches again when the
    /// language has changed since the last load.
    /// </summary>
    /// <remarks>
    /// Keyed on the language the header will carry, not the bundle on screen: if the bundle
    /// download failed, the UI falls back to English while the server still translates into the
    /// chosen language, and it is the server's answer being cached here.
    /// </remarks>
    public async Task EnsureLoadedAsync()
    {
        await EnsureLanguageAsync();

        if (_loadTask is null || _failed || !SameCode(_loadedCode, _language.Code))
            StartLoad();

        await FollowLatestAsync();
    }

    /// <summary>Fetches again regardless of the cache — the "an administrator edited the modules"
    /// signal from <see cref="MenuRefreshService"/>.</summary>
    public async Task ReloadAsync()
    {
        await EnsureLanguageAsync();
        StartLoad();
        await FollowLatestAsync();
    }

    /// <summary>
    /// Whether the menu reaches <paramref name="route"/>; "sadhana" and "/sadhana" alike. False
    /// while the menu is still loading, so a gated shortcut appears late rather than disappearing
    /// from under a thumb.
    /// </summary>
    public bool CanOpen(string route) => Menu is not null && MobileMenu.Contains(Menu, route);

    /// <summary>Forgets the previous user's menu; the next <see cref="EnsureLoadedAsync"/> fetches
    /// the new user's, whose role may open a different set of forms.</summary>
    public void Reset()
    {
        _generation++;          // abandons any load still in flight for the previous user
        _loadTask = null;
        _loadedCode = null;
        _failed = false;
        Menu = null;
        OnChanged?.Invoke();
    }

    /// <summary>
    /// The X-App-Language header is only right once localization has loaded; a menu fetched
    /// before that would be cached under the wrong language.
    /// </summary>
    private async Task EnsureLanguageAsync()
    {
        try { await _loc.EnsureLoadedAsync(); }
        catch { /* the header falls back to the stored preference; the menu still loads */ }
    }

    private void StartLoad()
    {
        var generation = ++_generation;
        _loadedCode = _language.Code;
        _failed = false;
        _loadTask = LoadAsync(generation);
    }

    private async Task LoadAsync(int generation)
    {
        // An empty menu is the honest answer to "the API is unreachable": the bottom bar then
        // shows its offline state rather than a row of tabs that would 404.
        // It is remembered as a failure, not as the answer, so the next EnsureLoadedAsync asks again.
        // The flag rather than nulling _loadTask: this method can complete synchronously, and
        // StartLoad would then store the finished task over the null.
        List<MenuTreeNode> menu;
        var failed = false;
        try { menu = await _api.GetMenuAsync(); }
        catch { menu = new(); failed = true; }

        if (generation != _generation) return;   // superseded by a newer load or a sign-in

        _failed = failed;
        Menu = menu;
        OnChanged?.Invoke();
    }

    /// <summary>
    /// Waits for the newest load, not the one this caller joined: a load superseded mid-flight
    /// writes nothing, and returning when it finished would hand the caller a menu that has not
    /// arrived yet. Returns early after a <see cref="Reset"/>, which leaves nothing to wait for.
    /// </summary>
    private async Task FollowLatestAsync()
    {
        while (_loadTask is { } task)
        {
            await task;
            if (ReferenceEquals(task, _loadTask)) return;
        }
    }

    private static bool SameCode(string? a, string? b)
        => string.Equals(a ?? string.Empty, b ?? string.Empty, StringComparison.OrdinalIgnoreCase);
}
