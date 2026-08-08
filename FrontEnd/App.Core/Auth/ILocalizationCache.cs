namespace App.Core.Auth;

/// <summary>
/// Per-platform storage for the last translation bundle we successfully downloaded
/// (web = localStorage, mobile = a file in app data).
/// </summary>
/// <remarks>
/// This is what makes the app usable offline. The bundle is 25–60 KB of pure display text with no
/// user data in it, so caching it on the device is safe; it is re-fetched in the background on
/// every start so an edit made in Language Configs still reaches the device on its next launch.
/// </remarks>
public interface ILocalizationCache
{
    Task<string?> GetAsync(string languageCode);
    Task SetAsync(string languageCode, string json);
}
