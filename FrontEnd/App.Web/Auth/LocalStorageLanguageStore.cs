using App.Core.Auth;
using Microsoft.JSInterop;

namespace App.Web.Auth;

public class LocalStorageLanguageStore : ILanguageStore
{
    private const string Key = "sc-lang";
    private readonly IJSRuntime _js;

    public LocalStorageLanguageStore(IJSRuntime js) => _js = js;

    public async Task<string?> GetLanguageAsync() => await _js.InvokeAsync<string?>("localStorage.getItem", Key);

    public async Task SetLanguageAsync(string languageCode) => await _js.InvokeVoidAsync("localStorage.setItem", Key, languageCode);
}
