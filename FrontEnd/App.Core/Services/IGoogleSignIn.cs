using Microsoft.AspNetCore.Components;

namespace App.Core.Services;

/// <summary>How a host presents Google sign-in, which decides what the shared button component renders.</summary>
public enum GoogleSignInMode
{
    /// <summary>This host cannot obtain a Google ID token; the button is not rendered at all.</summary>
    None,

    /// <summary>Google's own JavaScript library draws the button and hands back the token (web).</summary>
    Button,

    /// <summary>The app draws its own button and a native call returns the token (Android).</summary>
    Native
}

/// <summary>
/// Obtains a Google ID token the API can verify, in whichever way the host allows.
/// </summary>
/// <remarks>
/// Three hosts, three answers, one interface — the same pattern as <see cref="IGeolocationProvider"/>.
/// The browser uses Google Identity Services, which insists on drawing the button itself. Android
/// uses the Credential Manager sheet, because Google no longer lets new Android apps run the OAuth
/// redirect through a browser. iOS gets <see cref="GoogleSignInMode.None"/> for now: offering
/// Google there obliges the app to offer Sign in with Apple as well (App Store guideline 4.8), which
/// is a separate piece of work. The shared UI asks <see cref="Mode"/> and never knows which it got.
/// </remarks>
public interface IGoogleSignIn
{
    GoogleSignInMode Mode { get; }

    /// <summary>
    /// Button mode only: draws Google's button into <paramref name="container"/> and invokes
    /// <paramref name="onIdToken"/> when the seeker completes the Google prompt.
    /// </summary>
    Task RenderButtonAsync(ElementReference container, Func<string, Task> onIdToken);

    /// <summary>Native mode only: shows the platform's account picker and returns the ID token, or null if the seeker backed out.</summary>
    Task<string?> GetIdTokenAsync(CancellationToken cancellationToken = default);
}

/// <summary>The answer for a host that offers no Google sign-in (iOS, Mac Catalyst, Windows, an unconfigured web build).</summary>
public sealed class UnavailableGoogleSignIn : IGoogleSignIn
{
    public GoogleSignInMode Mode => GoogleSignInMode.None;

    public Task RenderButtonAsync(ElementReference container, Func<string, Task> onIdToken) => Task.CompletedTask;

    public Task<string?> GetIdTokenAsync(CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);
}
