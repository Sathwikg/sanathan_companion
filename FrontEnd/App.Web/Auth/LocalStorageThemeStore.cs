using App.Core.Auth;
using Microsoft.JSInterop;

namespace App.Web.Auth;

public class LocalStorageThemeStore : IThemeStore
{
    private const string Key = "sc-theme";
    private readonly IJSRuntime _js;

    public LocalStorageThemeStore(IJSRuntime js) => _js = js;

    public async Task<string?> GetThemeAsync() => await _js.InvokeAsync<string?>("localStorage.getItem", Key);
    public async Task SetThemeAsync(string theme) => await _js.InvokeVoidAsync("localStorage.setItem", Key, theme);
}
