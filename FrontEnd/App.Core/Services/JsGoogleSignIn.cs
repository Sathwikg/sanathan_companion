using App.Core.Config;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace App.Core.Services;

/// <summary>
/// Google sign-in for the browser, over the <c>scGoogle</c> wrapper in googleSignIn.js.
/// </summary>
/// <remarks>
/// Google Identity Services loads on demand, from Google, the first time a login page asks for the
/// button: the script is not part of the app bundle, so a seeker who never touches the button never
/// fetches it, and the MAUI host (which has no use for it) never even links it. The library draws
/// the button itself and calls back with the ID token; the callback crosses into .NET through a
/// <see cref="DotNetObjectReference{TValue}"/> that lives exactly as long as this service.
/// </remarks>
public sealed class JsGoogleSignIn : IGoogleSignIn, IAsyncDisposable
{
    private readonly IJSRuntime _js;
    private readonly AppConfig _config;
    private DotNetObjectReference<Callback>? _callback;

    public JsGoogleSignIn(IJSRuntime js, AppConfig config)
    {
        _js = js;
        _config = config;
    }

    public GoogleSignInMode Mode => _config.GoogleSignInConfigured ? GoogleSignInMode.Button : GoogleSignInMode.None;

    public async Task RenderButtonAsync(ElementReference container, Func<string, Task> onIdToken)
    {
        if (Mode != GoogleSignInMode.Button) return;

        _callback?.Dispose();
        _callback = DotNetObjectReference.Create(new Callback(onIdToken));

        await _js.InvokeVoidAsync("scGoogle.render", container, _config.GoogleClientId, _callback);
    }

    public Task<string?> GetIdTokenAsync(CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);

    public ValueTask DisposeAsync()
    {
        _callback?.Dispose();
        _callback = null;
        return ValueTask.CompletedTask;
    }

    /// <summary>The object googleSignIn.js calls back into. Public because JSInterop requires it.</summary>
    public sealed class Callback
    {
        private readonly Func<string, Task> _onIdToken;

        public Callback(Func<string, Task> onIdToken) => _onIdToken = onIdToken;

        [JSInvokable]
        public Task OnCredential(string idToken) => _onIdToken(idToken);
    }
}
