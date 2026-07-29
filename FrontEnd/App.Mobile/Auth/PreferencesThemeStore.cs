using App.Core.Auth;

namespace App.Mobile.Auth;

public class PreferencesThemeStore : IThemeStore
{
    private const string Key = "sc-theme";

    public Task<string?> GetThemeAsync()
        => Task.FromResult(Preferences.Default.ContainsKey(Key) ? Preferences.Default.Get(Key, "light") : null);

    public Task SetThemeAsync(string theme)
    {
        Preferences.Default.Set(Key, theme);
        return Task.CompletedTask;
    }
}
