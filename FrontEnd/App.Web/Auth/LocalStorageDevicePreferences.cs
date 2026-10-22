using App.Core.Services;
using Microsoft.JSInterop;

namespace App.Web.Auth;

public class LocalStorageDevicePreferences : IDevicePreferences
{
    private readonly IJSRuntime _js;

    public LocalStorageDevicePreferences(IJSRuntime js) => _js = js;

    public async Task<bool> GetFlagAsync(string key, bool fallback = false)
    {
        var value = await _js.InvokeAsync<string?>("localStorage.getItem", key);
        return value is null ? fallback : value == "1";
    }

    public async Task SetFlagAsync(string key, bool value)
        => await _js.InvokeVoidAsync("localStorage.setItem", key, value ? "1" : "0");
}
