using App.Core.Auth;
using Microsoft.JSInterop;

namespace App.Web.Auth;

public class LocalStorageTokenStore : ITokenStore
{
    private const string AccessKey = "sc-token";
    private const string RefreshKey = "sc-refresh";

    private readonly IJSRuntime _js;

    public LocalStorageTokenStore(IJSRuntime js) => _js = js;

    public async Task<string?> GetTokenAsync() => await _js.InvokeAsync<string?>("localStorage.getItem", AccessKey);

    public async Task<string?> GetRefreshTokenAsync() => await _js.InvokeAsync<string?>("localStorage.getItem", RefreshKey);

    public async Task SetTokensAsync(string accessToken, string refreshToken)
    {
        await _js.InvokeVoidAsync("localStorage.setItem", AccessKey, accessToken);
        await _js.InvokeVoidAsync("localStorage.setItem", RefreshKey, refreshToken);
    }

    public async Task ClearTokenAsync()
    {
        await _js.InvokeVoidAsync("localStorage.removeItem", AccessKey);
        await _js.InvokeVoidAsync("localStorage.removeItem", RefreshKey);
    }
}
