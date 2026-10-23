using App.Core.Auth;
using Microsoft.JSInterop;

namespace App.Web.Auth;

public class LocalStorageGuestModeStore : IGuestModeStore
{
    private const string Key = "sc-guest";
    private readonly IJSRuntime _js;

    public LocalStorageGuestModeStore(IJSRuntime js) => _js = js;

    public async Task<bool> IsGuestAsync()
        => await _js.InvokeAsync<string?>("localStorage.getItem", Key) == "1";

    public async Task SetGuestAsync(bool isGuest)
    {
        if (isGuest) await _js.InvokeVoidAsync("localStorage.setItem", Key, "1");
        else await _js.InvokeVoidAsync("localStorage.removeItem", Key);
    }
}
