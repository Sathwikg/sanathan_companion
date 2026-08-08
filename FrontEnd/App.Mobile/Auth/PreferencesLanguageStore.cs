using App.Core.Auth;

namespace App.Mobile.Auth;

public class PreferencesLanguageStore : ILanguageStore
{
    private const string Key = "sc-lang";

    public Task<string?> GetLanguageAsync()
        => Task.FromResult(Preferences.Default.ContainsKey(Key) ? Preferences.Default.Get(Key, "en") : null);

    public Task SetLanguageAsync(string languageCode)
    {
        Preferences.Default.Set(Key, languageCode);
        return Task.CompletedTask;
    }
}
