using Android.App;
using Android.Content;
using Android.Graphics;
using AndroidX.Core.App;
using AndroidX.Core.Content;

namespace App.Mobile.Platforms.Android.Notifications;

/// <summary>What one notification says and where a tap on it leads.</summary>
/// <param name="Tag">Notifications with the same tag replace one another in the shade.</param>
/// <param name="Channel">One of <see cref="AppNotifications"/>'s channel ids.</param>
/// <param name="Route">The in-app page a tap opens; null opens the home.</param>
/// <param name="ImageUrl">Optional https picture, shown as the thumbnail and, expanded, large.</param>
internal sealed record AppNotification(string Tag, string Title, string? Body, string Channel, string? Route = null, string? ImageUrl = null);

/// <summary>
/// The one place the Android app draws a notification, so a daily reminder scheduled on the phone
/// and a push sent by the server look the same: the diya in the status bar, the app's saffron as the
/// accent, the category beside the app's name, the whole text when expanded, and a tap that opens
/// the page it is about.
/// </summary>
/// <remarks>
/// When the app is closed, a push is drawn by Google Play services instead, from the notification
/// part of the message. The manifest names the same icon, colour and fallback channel, and the
/// server names the channel per message, so the two renderings match.
/// </remarks>
internal static class AppNotifications
{
    // Channel ids. The four server-sent ones are spelled exactly as the server's
    // NotificationChannels constants, because the server names the channel in each push.
    internal const string Sadhana = "sadhana";
    internal const string Panchangam = "panchangam";
    internal const string Festivals = "festivals";
    internal const string General = "general";

    /// <summary>The plain daily reminders the phone schedules itself. Kept from before push existed, so the seeker's own setting for it survives.</summary>
    internal const string Reminders = "sanathana.reminders";

    /// <summary>The extra a tap carries to MainActivity. Also the key the server puts the page under in a push's data.</summary>
    internal const string ExtraRoute = "route";

    private sealed record ChannelSpec(string Id, string Name, string Description, NotificationImportance Importance);

    // Channels are the seeker's own switches in Android's settings, one per kind of thing, so
    // someone who wants the Rahu Kalam warning but not the festival notes can say so there too.
    // Panchangam is High because its warnings are about the next ten minutes and should be seen;
    // the rest arrive quietly in the shade.
    private static readonly ChannelSpec[] Channels =
    {
        new(Sadhana, "Sadhana", "Your daily sadhana reminder and notices about your streak.", NotificationImportance.Default),
        new(Panchangam, "Panchangam", "The morning Panchangam, and warnings before Rahu Kalam and the day's other windows.", NotificationImportance.High),
        new(Festivals, "Festivals & Calendar", "Festivals in your region and the days you marked on your own calendar.", NotificationImportance.Default),
        new(General, "Announcements", "Occasional news about the app.", NotificationImportance.Default),
        new(Reminders, "Daily Reminders", "Reminders you set for a time of day.", NotificationImportance.Default),
    };

    /// <summary>
    /// Creates every channel that does not exist yet. Called at app start, because a push that
    /// arrives while the app is closed is drawn by the system and needs its channel to be there.
    /// </summary>
    internal static void EnsureChannels(Context context)
    {
        // Channels only exist from Oreo; below that the importance lives on the notification.
        if (!OperatingSystem.IsAndroidVersionAtLeast(26)) return;

        try
        {
            var manager = (NotificationManager?)context.GetSystemService(Context.NotificationService);
            if (manager is null) return;

            foreach (var spec in Channels)
            {
                // Never recreated: once a channel exists its importance and sound belong to the seeker.
                if (manager.GetNotificationChannel(spec.Id) is not null) continue;

                manager.CreateNotificationChannel(new NotificationChannel(spec.Id, spec.Name, spec.Importance) { Description = spec.Description });
            }
        }
        catch (Java.Lang.Exception)
        {
            // Run from Application.OnCreate: nothing here may stop the app from starting.
        }
    }

    /// <summary>Posts one notification. Safe to call from a broadcast receiver or the messaging service: it never throws.</summary>
    internal static void Show(Context context, AppNotification notification)
    {
        try
        {
            EnsureChannels(context);

            var channel = Channels.Any(c => c.Id == notification.Channel) ? notification.Channel : General;
            var code = global::App.Core.Services.ReminderPlanner.StableCode(notification.Tag);
            var builder = new NotificationCompat.Builder(context, channel);

            // Each setter returns a nullable Builder in the binding, so the chain is unrolled and
            // the results discarded rather than dereferenced.
            builder.SetSmallIcon(SmallIcon(context));
            builder.SetColor(ContextCompat.GetColor(context, AccentColor(context)));
            builder.SetContentTitle(notification.Title);
            builder.SetAutoCancel(true);
            builder.SetShowWhen(true);
            builder.SetVisibility(NotificationCompat.VisibilityPublic);
            builder.SetCategory(channel == General ? NotificationCompat.CategoryMessage : NotificationCompat.CategoryReminder);

            // Below Oreo there are no channels, so the importance is stated here.
            builder.SetPriority(channel == Panchangam ? (int)NotificationPriority.High : (int)NotificationPriority.Default);

            // The category beside the app's name in the header, as in "App Name · Panchangam".
            var label = Channels.First(c => c.Id == channel).Name;
            builder.SetSubText(label);

            var picture = Download(notification.ImageUrl);
            if (picture is not null) builder.SetLargeIcon(picture);

            if (!string.IsNullOrWhiteSpace(notification.Body))
            {
                builder.SetContentText(notification.Body);

                if (picture is not null)
                {
                    // Expanded: the picture large, with the thumbnail hidden so it is not shown twice.
                    var style = new NotificationCompat.BigPictureStyle();
                    style.BigPicture(picture);
                    style.BigLargeIcon((Bitmap?)null);
                    style.SetSummaryText(notification.Body);
                    builder.SetStyle(style);
                }
                else
                {
                    // Expanded: the whole text rather than one clipped line.
                    builder.SetStyle(new NotificationCompat.BigTextStyle().BigText(notification.Body));
                }
            }

            var launch = context.PackageManager?.GetLaunchIntentForPackage(context.PackageName ?? string.Empty);
            if (launch is not null)
            {
                launch.SetFlags(ActivityFlags.SingleTop | ActivityFlags.ClearTop);
                if (!string.IsNullOrWhiteSpace(notification.Route)) launch.PutExtra(ExtraRoute, notification.Route);

                // Immutable: nothing downstream rewrites the intent, and Android 12+ requires one of
                // Mutable/Immutable to be stated. The request code differs per tag so two
                // notifications in the shade keep their own pages.
                var pending = PendingIntent.GetActivity(context, code, launch, PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);
                if (pending is not null) builder.SetContentIntent(pending);
            }

            // Build() and From() are bound as nullable; neither is a real outcome, but a receiver
            // is the wrong place to find out otherwise.
            var built = builder.Build();
            var manager = NotificationManagerCompat.From(context);
            if (built is not null && manager is not null) manager.Notify(code, built);
        }
        catch (System.Exception)
        {
            // Posting can be refused (permission revoked between scheduling and firing), and a
            // malformed picture or a missing resource must not take the process down with it. A
            // missed notification is the worst that may happen here.
        }
    }

    /// <summary>
    /// The diya. Looked up by name rather than through the generated Resource class so this file
    /// compiles the same whichever way the resource designer is configured; the old system "info"
    /// glyph is the fallback if the drawable is ever removed.
    /// </summary>
    private static int SmallIcon(Context context)
    {
        var id = context.Resources?.GetIdentifier("ic_notification", "drawable", context.PackageName) ?? 0;
        return id != 0 ? id : global::Android.Resource.Drawable.IcDialogInfo;
    }

    /// <summary>The app's saffron from colors.xml (notification_accent), which is also what the manifest names for system-drawn pushes.</summary>
    private static int AccentColor(Context context)
    {
        var id = context.Resources?.GetIdentifier("notification_accent", "color", context.PackageName) ?? 0;
        return id != 0 ? id : global::Android.Resource.Color.HoloOrangeDark;
    }

    /// <summary>
    /// Fetches the notification's picture, or returns null: no address, not https, too slow, too
    /// large, not an image. A notification is worth showing without its picture and not worth
    /// delaying for it, so every failure is silent and the wait is short.
    /// </summary>
    private static Bitmap? Download(string? url)
    {
        if (string.IsNullOrWhiteSpace(url) || !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) return null;

        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(6) };
            http.MaxResponseContentBufferSize = 2 * 1024 * 1024;

            // Called from the messaging service's worker thread or a receiver, never the UI thread.
            var bytes = http.GetByteArrayAsync(url).GetAwaiter().GetResult();
            return BitmapFactory.DecodeByteArray(bytes, 0, bytes.Length);
        }
        catch (System.Exception)
        {
            return null;
        }
    }
}
