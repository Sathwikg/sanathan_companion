using Android.App;
using Android.Content;
using Android.OS;
using AndroidX.Core.App;

namespace App.Mobile.Platforms.Android.Notifications;

/// <summary>Everything the Android side needs to describe and post a reminder.</summary>
internal static class ReminderNotifier
{
    /// <summary>
    /// One channel for every reminder. Channels are the user's control surface on Android 8+, and
    /// a single "Daily reminders" switch matches what the app already offers in-app; a channel per
    /// notification type would hand the seeker two competing sets of switches.
    /// </summary>
    internal const string ChannelId = "sanathana.reminders";

    internal const string ExtraTitle = "title";
    internal const string ExtraBody = "body";
    internal const string ExtraId = "id";

    internal static void EnsureChannel(Context context)
    {
        // Channels only exist from Oreo; below that the importance lives on the notification.
        if (!OperatingSystem.IsAndroidVersionAtLeast(26)) return;

        var manager = (NotificationManager?)context.GetSystemService(Context.NotificationService);
        if (manager is null || manager.GetNotificationChannel(ChannelId) is not null) return;

        // Default importance, not High: a sadhana reminder should appear quietly in the shade, not
        // interrupt with a heads-up banner over whatever the seeker is doing.
        var channel = new NotificationChannel(
            ChannelId,
            "Daily reminders",
            NotificationImportance.Default)
        {
            Description = "Reminders for your sadhana, panchangam and pujas."
        };

        manager.CreateNotificationChannel(channel);
    }

    /// <summary>Posts one reminder, opening the app when tapped.</summary>
    internal static void Show(Context context, string id, string title, string? body)
    {
        EnsureChannel(context);

        var code = RequestCode(id);
        var builder = new NotificationCompat.Builder(context, ChannelId);

        // Each setter returns a nullable Builder in the binding, so the chain is unrolled and the
        // result discarded rather than dereferenced — this runs inside a BroadcastReceiver, where
        // a NullReferenceException would be an app-wide crash for a missed reminder.
        builder.SetContentTitle(title);
        builder.SetSmallIcon(global::Android.Resource.Drawable.IcDialogInfo);
        builder.SetAutoCancel(true);
        builder.SetPriority((int)NotificationPriority.Default);

        if (!string.IsNullOrWhiteSpace(body))
        {
            builder.SetContentText(body);
            builder.SetStyle(new NotificationCompat.BigTextStyle().BigText(body));
        }

        var launch = context.PackageManager?.GetLaunchIntentForPackage(context.PackageName ?? string.Empty);
        if (launch is not null)
        {
            launch.SetFlags(ActivityFlags.SingleTop | ActivityFlags.ClearTop);

            // Immutable: nothing downstream rewrites the intent, and Android 12+ requires one of
            // Mutable/Immutable to be stated explicitly.
            var pending = PendingIntent.GetActivity(
                context, code, launch, PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);

            if (pending is not null) builder.SetContentIntent(pending);
        }

        try
        {
            // Build() is bound as nullable; a null notification is not a real outcome, but guarding
            // is cheaper than a crash in a receiver.
            // Both Build() and From() are bound as nullable; neither is a real outcome, but a
            // receiver is the wrong place to find out otherwise.
            var notification = builder.Build();
            var manager = NotificationManagerCompat.From(context);
            if (notification is not null && manager is not null)
                manager.Notify(code, notification);
        }
        catch (System.Exception)
        {
            // Posting can still be refused (permission revoked between scheduling and firing).
            // A missed reminder must never take the process down from a broadcast receiver.
        }
    }

    /// <summary>
    /// The int Android uses for both the PendingIntent and the posted notification.
    /// </summary>
    /// <remarks>
    /// Delegates to App.Core so the derivation is shared and unit-tested — see
    /// <see cref="global::App.Core.Services.ReminderPlanner.StableCode"/> for why it cannot be
    /// string.GetHashCode().
    /// </remarks>
    internal static int RequestCode(string id) => global::App.Core.Services.ReminderPlanner.StableCode(id);
}
