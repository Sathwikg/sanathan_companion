using App.Core.Auth;
using App.Core.Models;

namespace App.Core.Services;

/// <summary>
/// App-wide translation state: the selected language, its bundle, and the lookup used by every
/// component. Modelled on <see cref="RegionState"/> — idempotent load, an <see cref="OnChanged"/>
/// event components re-render on, and a reset hook for user switches.
/// </summary>
/// <remarks>
/// Deliberately independent of <see cref="RegionState"/>: the display language is a personal
/// preference and must not change when the user switches region.
/// <para>
/// Intentionally NOT an <see cref="IUserSessionState"/>. It caches no user data — the bundle is
/// public display text — and the chosen language is a device preference that should survive
/// signing out rather than flashing back to English.
/// </para>
/// </remarks>
public class LocalizationState
{
    public const string BaseCode = "en";

    private readonly IApiClient _api;
    private readonly ILanguageStore _store;
    private readonly LanguageContext _context;
    private readonly ILocalizationCache? _cache;
    private Task? _loadTask;

    public LocalizationState(IApiClient api, ILanguageStore store, LanguageContext context, ILocalizationCache? cache = null)
    {
        _api = api;
        _store = store;
        _context = context;
        _cache = cache;
    }

    /// <summary>
    /// Incremented whenever the language changes. Database text is translated server-side, so a
    /// re-render alone is not enough — already-fetched data is stale and must be requested again.
    /// </summary>
    public int DataVersion { get; private set; }

    /// <summary>Languages offered in the switcher.</summary>
    public List<LocaleModel> Locales { get; private set; } = new();

    /// <summary>The active bundle. Starts empty, which makes every lookup fall back to its key's default.</summary>
    public LocalizationBundle Bundle { get; private set; } = new();

    public string CurrentCode => Bundle.Code;
    public bool IsBase => Bundle.IsBase || Bundle.Code == BaseCode;

    /// <summary>Raised after the language changes so subscribers can re-render.</summary>
    public event Action? OnChanged;

    /// <summary>Safe to call from many components; the work happens once.</summary>
    public Task EnsureLoadedAsync() => _loadTask ??= LoadAsync();

    private async Task LoadAsync()
    {
        // Whether the server actually answered. Empty-because-offline and
        // empty-because-there-are-none must not be treated the same way.
        var haveServerLocales = false;
        try
        {
            Locales = await _api.GetLocalesAsync();
            haveServerLocales = Locales.Count > 0;
            if (haveServerLocales) await WriteLocalesCacheAsync(Locales);
        }
        catch
        {
            Locales = new List<LocaleModel>();
        }

        // Offline: fall back to the last locale list we saw so the switcher still works.
        if (!haveServerLocales)
            Locales = await ReadLocalesCacheAsync() ?? new List<LocaleModel>();

        var stored = await SafeGetStoredAsync();
        var code = !string.IsNullOrWhiteSpace(stored) ? stored! : BaseCode;

        // Only second-guess the stored language when the server actually told us what exists —
        // otherwise a dropped connection would silently reset the user back to English.
        if (haveServerLocales &&
            !Locales.Any(l => string.Equals(l.Code, code, StringComparison.OrdinalIgnoreCase)))
            code = BaseCode;

        await ApplyAsync(code, persist: false);
    }

    /// <summary>Switches language, persists the choice and notifies the app.</summary>
    public async Task SelectAsync(string code)
    {
        if (string.IsNullOrWhiteSpace(code)) return;
        if (string.Equals(code, Bundle.Code, StringComparison.OrdinalIgnoreCase)) return;

        await ApplyAsync(code, persist: true);

        // Bumped only on a real user-initiated switch, not on first load — the layout keys the
        // page on this, so bumping during startup would discard the page's very first render.
        DataVersion++;
        OnChanged?.Invoke();
    }

    /// <summary>
    /// Shows the cached bundle first so the UI is translated immediately (and works with no
    /// network at all), then refreshes from the API so edits made in Language Configs land.
    /// </summary>
    private async Task ApplyAsync(string code, bool persist)
    {
        // Set BEFORE any fetch below, so the bundle request — and every page reload that this
        // change triggers — already carries the new language header.
        _context.Code = code;

        if (persist)
        {
            try { await _store.SetLanguageAsync(code); } catch { /* preference is best-effort */ }
        }

        var shownFromCache = false;
        var cached = await ReadCacheAsync(code);
        if (cached is not null)
        {
            Bundle = cached;
            shownFromCache = true;
            OnChanged?.Invoke();
        }

        LocalizationBundle? fresh = null;
        try { fresh = await _api.GetLocalizationBundleAsync(code); }
        catch { /* offline or server down — the cache (or English) carries the UI */ }

        if (fresh is not null)
        {
            Bundle = fresh;
            await WriteCacheAsync(code, fresh);
            OnChanged?.Invoke();
        }
        else if (!shownFromCache)
        {
            // Nothing cached and nothing downloaded: fall back to the English literals in the markup.
            Bundle = new LocalizationBundle { Code = BaseCode, IsBase = true };
            OnChanged?.Invoke();
        }
    }

    private async Task<LocalizationBundle?> ReadCacheAsync(string code)
    {
        if (_cache is null) return null;
        try
        {
            var json = await _cache.GetAsync(code);
            return string.IsNullOrWhiteSpace(json)
                ? null
                : System.Text.Json.JsonSerializer.Deserialize<LocalizationBundle>(json, JsonOptions);
        }
        catch
        {
            return null; // a corrupt cache entry is just a miss
        }
    }

    private async Task WriteCacheAsync(string code, LocalizationBundle bundle)
    {
        if (_cache is null) return;
        try { await _cache.SetAsync(code, System.Text.Json.JsonSerializer.Serialize(bundle, JsonOptions)); }
        catch { /* best-effort */ }
    }

    /// <summary>Cache key for the locale list; not a language code, so it cannot collide with one.</summary>
    private const string LocalesCacheKey = "__locales";

    private async Task<List<LocaleModel>?> ReadLocalesCacheAsync()
    {
        if (_cache is null) return null;
        try
        {
            var json = await _cache.GetAsync(LocalesCacheKey);
            return string.IsNullOrWhiteSpace(json)
                ? null
                : System.Text.Json.JsonSerializer.Deserialize<List<LocaleModel>>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    private async Task WriteLocalesCacheAsync(List<LocaleModel> locales)
    {
        if (_cache is null) return;
        try { await _cache.SetAsync(LocalesCacheKey, System.Text.Json.JsonSerializer.Serialize(locales, JsonOptions)); }
        catch { /* best-effort */ }
    }

    /// <summary>Matches the camelCase the API emits so a cached bundle round-trips exactly.</summary>
    private static readonly System.Text.Json.JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private async Task<string?> SafeGetStoredAsync()
    {
        try { return await _store.GetLanguageAsync(); }
        catch { return null; }
    }

    // ---------------------------------------------------------------- lookup

    /// <summary>
    /// Translates a label. <paramref name="fallback"/> is the English text written at the call
    /// site, so an untranslated key still renders correctly instead of showing the raw key.
    /// </summary>
    public string T(string key, string? fallback = null)
        => Bundle.Labels.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v)
            ? v
            : fallback ?? key;

    /// <summary>Indexer form so markup can read <c>@Loc["common.save", "Save"]</c>.</summary>
    public string this[string key] => T(key);
    public string this[string key, string fallback] => T(key, fallback);

    /// <summary>Translates and fills {0}, {1}… placeholders.</summary>
    public string TF(string key, string fallback, params object?[] args)
    {
        var template = T(key, fallback);
        try { return string.Format(template, args); }
        catch (FormatException) { return template; }
    }

    /// <summary>
    /// Translates a value that came from the database (a menu name, deity name…).
    /// Falls back to the stored English original when no translation exists.
    /// </summary>
    public string Entity(string entityType, object entityKey, string field, string? original)
    {
        if (string.IsNullOrWhiteSpace(original)) return original ?? string.Empty;
        var key = $"{entityType}:{entityKey}:{field}";
        return Bundle.Entities.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v) ? v : original!;
    }

    /// <summary>
    /// False when an admin has opted this route out of the current language, in which case the
    /// page should render its English text. Always true for English itself.
    /// </summary>
    public bool IsRouteTranslated(string? route)
    {
        if (IsBase || Bundle.DisabledRoutes.Count == 0) return true;
        var normalised = Normalise(route);
        if (normalised.Length == 0) return true;
        return !Bundle.DisabledRoutes.Any(r => string.Equals(Normalise(r), normalised, StringComparison.OrdinalIgnoreCase));
    }

    private static string Normalise(string? route) => (route ?? string.Empty).Trim().Trim('/').ToLowerInvariant();

    /// <summary>Forces the next <see cref="EnsureLoadedAsync"/> to refetch (used after editing translations).</summary>
    public async Task ReloadAsync()
    {
        _loadTask = null;
        await EnsureLoadedAsync();
        OnChanged?.Invoke();
    }
}
