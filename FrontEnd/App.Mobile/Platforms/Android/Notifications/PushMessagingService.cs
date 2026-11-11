using Android.App;
using App.Core.Services;
using Firebase.Messaging;

namespace App.Mobile.Platforms.Android.Notifications;

/// <summary>
/// Receives pushes from Firebase Cloud Messaging while the app's process is alive, and hears when
/// Firebase replaces this installation's token.
/// </summary>
/// <remarks>
/// Android starts this service for a message in two cases: the message is data-only, or the app is
/// in the foreground. In both the app draws the notification itself, through
/// <see cref="AppNotifications"/>. A message with a notification part that arrives while the app is
/// in the background never reaches here: Google Play services draws it, from the icon, colour and
/// channel the manifest and the message name. Tapping either kind starts MainActivity with the
/// page to open.
/// <para>
/// Not exported: only Google Play services, through Firebase's own receiver, delivers to it.
/// </para>
/// </remarks>
[Service(Exported = false)]
[IntentFilter(new[] { "com.google.firebase.MESSAGING_EVENT" })]
public class PushMessagingService : FirebaseMessagingService
{
    public override void OnMessageReceived(RemoteMessage message)
    {
        try
        {
            var data = message.Data ?? new Dictionary<string, string>();
            var shown = message.GetNotification();

            // The server puts everything in the data part of every message; the notification part,
            // when there is one, repeats the title and body for the system's own drawing.
            var title = Value(data, "title") ?? shown?.Title;
            if (string.IsNullOrWhiteSpace(title)) return;

            var kind = Value(data, "kind") ?? "general";

            AppNotifications.Show(this, new AppNotification(
                // Same type replaces its earlier one; an unknown message is at least unique.
                Tag: Value(data, "tag") ?? kind + ":" + (message.MessageId ?? Guid.NewGuid().ToString("N")),
                Title: title!,
                Body: Value(data, "body") ?? shown?.Body,
                Channel: Value(data, "channel") ?? AppNotifications.General,
                Route: Value(data, "route"),
                ImageUrl: Value(data, "image")));

            // The app is open (or this would not have been called for a notification message):
            // let the bell know something arrived.
            Services()?.GetService<DeepLinks>()?.MessageReceived();
        }
        catch (System.Exception)
        {
            // A malformed push must never crash the app it was sent to.
        }
    }

    /// <summary>
    /// Firebase has issued this installation a new token (first run, app data restored, or a
    /// periodic rotation). The app registers whatever token is current each time it starts with a
    /// signed-in seeker, so nothing needs sending from here; the old token simply stops working and
    /// the server retires it the first time a send to it is refused.
    /// </summary>
    // The binding marks this callback obsolete; Firebase itself does not, and it is the only way
    // the SDK reports a replaced token.
#pragma warning disable CS0672, CS0618
    public override void OnNewToken(string token)
    {
        base.OnNewToken(token);
        try { Services()?.GetService<DeepLinks>()?.TokenChanged(); }
        catch (System.Exception) { /* nothing to do: the next start registers it */ }
    }
#pragma warning restore CS0672, CS0618

    private static string? Value(IDictionary<string, string> data, string key)
        => data.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : null;

    /// <summary>The MAUI container, when the app has built one in this process.</summary>
    private static IServiceProvider? Services() => IPlatformApplication.Current?.Services;
}
