using App.Core.Auth;
using Microsoft.JSInterop;

namespace App.Web.Auth;

/// <summary>Caches the translation bundle in localStorage (a few tens of KB per language).</summary>
public class LocalStorageLocalizationCache : ILocalizationCache
{
    private readonly IJSRuntime _js;

    public LocalStorageLocalizationCache(IJSRuntime js) => _js = js;

    private static string KeyFor(string code) => $"sc-bundle-{code.ToLowerInvariant()}";

    public async Task<string?> GetAsync(string languageCode)
    {
        try { return await _js.InvokeAsync<string?>("localStorage.getItem", KeyFor(languageCode)); }
        catch { return null; }
    }

    public async Task SetAsync(string languageCode, string json)
    {
        // A full storage quota must never break the app — the bundle is only an optimisation.
        try { await _js.InvokeVoidAsync("localStorage.setItem", KeyFor(languageCode), json); }
        catch { /* ignored */ }
    }
}
