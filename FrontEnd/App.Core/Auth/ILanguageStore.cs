namespace App.Core.Auth;

/// <summary>Per-platform language persistence (web = localStorage, mobile = Preferences).</summary>
public interface ILanguageStore
{
    Task<string?> GetLanguageAsync();
    Task SetLanguageAsync(string languageCode);
}
