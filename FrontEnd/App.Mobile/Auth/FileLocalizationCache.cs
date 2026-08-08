using App.Core.Auth;

namespace App.Mobile.Auth;

/// <summary>
/// Caches the translation bundle as a file in app data. Preferences is the wrong store here —
/// it is meant for small values, and a bundle is 25–60 KB.
/// </summary>
public class FileLocalizationCache : ILocalizationCache
{
    private static string PathFor(string code)
        => Path.Combine(FileSystem.AppDataDirectory, $"sc-bundle-{code.ToLowerInvariant()}.json");

    public async Task<string?> GetAsync(string languageCode)
    {
        try
        {
            var path = PathFor(languageCode);
            return File.Exists(path) ? await File.ReadAllTextAsync(path) : null;
        }
        catch
        {
            return null; // an unreadable cache is simply a cache miss
        }
    }

    public async Task SetAsync(string languageCode, string json)
    {
        try { await File.WriteAllTextAsync(PathFor(languageCode), json); }
        catch { /* caching is best-effort */ }
    }
}
