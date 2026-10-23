using App.Core.Auth;

namespace App.Mobile.Auth;

/// <summary>
/// Plain Preferences rather than SecureStorage: this is a choice the visitor made, not a credential,
/// and reading it must not cost a Keystore decryption on every authentication-state check.
/// </summary>
public class PreferencesGuestModeStore : IGuestModeStore
{
    private const string Key = "sc-guest";

    public Task<bool> IsGuestAsync() => Task.FromResult(Preferences.Default.Get(Key, false));

    public Task SetGuestAsync(bool isGuest)
    {
        if (isGuest) Preferences.Default.Set(Key, true);
        else Preferences.Default.Remove(Key);
        return Task.CompletedTask;
    }
}
