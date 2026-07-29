using App.Core.Auth;

namespace App.Mobile.Auth;

public class SecureStorageTokenStore : ITokenStore
{
    private const string Key = "sc-token";

    public async Task<string?> GetTokenAsync()
    {
        try { return await SecureStorage.Default.GetAsync(Key); }
        catch { return null; } // some emulators lack a keystore
    }

    public Task SetTokenAsync(string token) => SecureStorage.Default.SetAsync(Key, token);

    public Task ClearTokenAsync()
    {
        SecureStorage.Default.Remove(Key);
        return Task.CompletedTask;
    }
}
