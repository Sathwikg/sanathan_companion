using App.Core.Services;

namespace App.Mobile.Auth;

public class PreferencesDevicePreferences : IDevicePreferences
{
    public Task<bool> GetFlagAsync(string key, bool fallback = false)
        => Task.FromResult(Preferences.Default.Get(key, fallback));

    public Task SetFlagAsync(string key, bool value)
    {
        Preferences.Default.Set(key, value);
        return Task.CompletedTask;
    }
}
