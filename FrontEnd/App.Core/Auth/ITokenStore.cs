namespace App.Core.Auth;

/// <summary>Per-platform JWT storage (web = localStorage, mobile = SecureStorage).</summary>
public interface ITokenStore
{
    Task<string?> GetTokenAsync();
    Task SetTokenAsync(string token);
    Task ClearTokenAsync();
}
