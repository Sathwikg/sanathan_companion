using Android.Gms.Extensions;
using App.Core.Services;
using Firebase;
using Firebase.Messaging;

namespace App.Mobile.Platforms.Android.Services;

/// <summary>
/// Asks Firebase Cloud Messaging for this installation's push token.
/// </summary>
/// <remarks>
/// Firebase configures itself from <c>google-services.json</c>, which the build turns into string
/// resources. A build made without that file (a fresh checkout, a CI run without the secret) has
/// nothing to configure from; that is reported as "no push here" and the app carries on with the
/// reminders it schedules itself, rather than failing at start-up over a missing file.
/// </remarks>
public sealed class AndroidPushRegistrar : IPushRegistrar
{
    /// <summary>1 on the server's PushPlatform enum.</summary>
    private const int AndroidPlatform = 1;

    public async Task<PushDevice?> GetDeviceAsync()
    {
        try
        {
            var context = global::Android.App.Application.Context;
            if (context is null) return null;

            // Null when the build carried no google-services.json: there is no default app to make.
            if (FirebaseApp.InitializeApp(context) is null) return null;

            // Throws when Google Play services is missing or too old (some phones sold without it).
            // Marked obsolete by the binding only; getToken() is Firebase's current way to ask.
#pragma warning disable CS0618
            var result = await FirebaseMessaging.Instance.GetToken().AsAsync<Java.Lang.String>();
#pragma warning restore CS0618
            var token = result?.ToString();
            if (string.IsNullOrWhiteSpace(token)) return null;

            return new PushDevice(
                AndroidPlatform,
                token,
                $"{DeviceInfo.Current.Manufacturer} {DeviceInfo.Current.Model}".Trim(),
                AppInfo.Current.VersionString,
                // An IANA id on Android ("Asia/Kolkata"), which is what the server looks up.
                TimeZoneInfo.Local.Id);
        }
        catch (System.Exception)
        {
            // No Play services, no network on first run, Firebase not configured: all mean the same
            // thing to the caller, and none of them is worth interrupting the seeker for.
            return null;
        }
    }
}
