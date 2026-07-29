using App.Core.Auth;
using Microsoft.JSInterop;

namespace App.Web.Auth;

public class LocalStorageTokenStore : ITokenStore
{
    private const string Key = "sc-token";
    private readonly IJSRuntime _js;

    public LocalStorageTokenStore(IJSRuntime js) => _js = js;

    public async Task<string?> GetTokenAsync() => await _js.InvokeAsync<string?>("localStorage.getItem", Key);
    public async Task SetTokenAsync(string token) => await _js.InvokeVoidAsync("localStorage.setItem", Key, token);
    public async Task ClearTokenAsync() => await _js.InvokeVoidAsync("localStorage.removeItem", Key);
}
