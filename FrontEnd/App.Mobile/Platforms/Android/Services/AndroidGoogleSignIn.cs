using Android.OS;
using Android.Runtime;
using AndroidX.Credentials;
using AndroidX.Credentials.Exceptions;
using App.Core.Config;
using App.Core.Services;
using Google.Android.Libraries.Identity.GoogleId;
using Java.Util.Concurrent;
using Microsoft.AspNetCore.Components;

namespace App.Mobile.Platforms.Android.Services;

/// <summary>
/// Google sign-in on Android through Credential Manager, the way Google now requires.
/// </summary>
/// <remarks>
/// The seeker sees the system account sheet, picks a Google account, and Google hands back an ID
/// token addressed to the API's "Web application" client id (the <c>serverClientId</c> below).
/// Nothing leaves the device but that token, and the API verifies it against Google's keys.
/// <para>
/// For the sheet to appear at all, an <em>Android</em> OAuth client must exist in Google Cloud
/// for this app's package name and signing certificate SHA-1 (debug keystore in development, the
/// Play App Signing key in production). That client's own id is never used in code; it is how
/// Google knows this APK is allowed to ask. Google Play services must be present on the device.
/// </para>
/// </remarks>
public sealed class AndroidGoogleSignIn : IGoogleSignIn
{
    private readonly AppConfig _config;

    public AndroidGoogleSignIn(AppConfig config) => _config = config;

    public GoogleSignInMode Mode => _config.GoogleSignInConfigured ? GoogleSignInMode.Native : GoogleSignInMode.None;

    public Task RenderButtonAsync(ElementReference container, Func<string, Task> onIdToken) => Task.CompletedTask;

    public async Task<string?> GetIdTokenAsync(CancellationToken cancellationToken = default)
    {
        if (Mode != GoogleSignInMode.Native) return null;

        var activity = Platform.CurrentActivity
                       ?? throw new InvalidOperationException("Google sign-in needs a foreground activity.");

        // FilterByAuthorizedAccounts=false: show every Google account on the device, not only the
        // ones that have used this app before. A first-time seeker has none, and with the filter
        // on the sheet would simply not appear.
        var option = new GetGoogleIdOption.Builder()
            .SetServerClientId(_config.GoogleClientId!)
            .SetFilterByAuthorizedAccounts(false)
            .SetAutoSelectEnabled(false)
            .Build();

        var request = new GetCredentialRequest.Builder()
            .AddCredentialOption(option)
            .Build();

        var completion = new TaskCompletionSource<string?>();
        using var cancel = new CancellationSignal();
        using var registration = cancellationToken.Register(() => cancel.Cancel());

        var manager = CredentialManager.Create(activity);
        manager.GetCredentialAsync(activity, request, cancel, Executors.NewSingleThreadExecutor()!, new Callback(completion));

        return await completion.Task;
    }

    /// <summary>Receives Credential Manager's answer on its executor and hands it to the awaiting task.</summary>
    private sealed class Callback : Java.Lang.Object, ICredentialManagerCallback
    {
        private readonly TaskCompletionSource<string?> _completion;

        public Callback(TaskCompletionSource<string?> completion) => _completion = completion;

        public void OnResult(Java.Lang.Object? result)
        {
            try
            {
                var credential = (result as GetCredentialResponse)?.Credential;
                if (credential is CustomCredential custom
                    && custom.Type == GoogleIdTokenCredential.TypeGoogleIdTokenCredential
                    && custom.Data is Bundle data)
                {
                    _completion.TrySetResult(GoogleIdTokenCredential.CreateFrom(data).IdToken);
                    return;
                }

                _completion.TrySetException(new InvalidOperationException("Google returned a credential that is not an ID token."));
            }
            catch (Exception ex)
            {
                _completion.TrySetException(ex);
            }
        }

        public void OnError(Java.Lang.Object? error)
        {
            // The callback's error type is erased to Object in the binding, so the Java throwable
            // arrives as a plain wrapper and a C# type test would never match. Java's own instanceof
            // tells the cases apart, and JavaCast re-wraps it as the bound exception for its message.
            //
            // Backing out of the sheet is the one non-error: the page treats null as "nothing
            // happened". Everything else — no Google account, Play services missing, a mismatched
            // signing certificate — surfaces as an exception the button shows as "not available".
            if (error is not null && Java.Lang.Class.FromType(typeof(GetCredentialCancellationException)).IsInstance(error))
            {
                _completion.TrySetResult(null);
                return;
            }

            _completion.TrySetException(new InvalidOperationException(Describe(error)));
        }

        private static string Describe(Java.Lang.Object? error)
        {
            const string fallback = "Google sign-in failed.";
            if (error is null) return fallback;
            try
            {
                return error.JavaCast<GetCredentialException>()?.Message ?? fallback;
            }
            catch (Exception)
            {
                return error.ToString() ?? fallback;
            }
        }
    }
}
