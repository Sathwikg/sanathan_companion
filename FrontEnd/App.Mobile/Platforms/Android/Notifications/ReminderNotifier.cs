using Android.App;
using Android.Content;
using Android.OS;
using AndroidX.Core.App;

namespace App.Mobile.Platforms.Android.Notifications;

/// <summary>Everything the Android side needs to describe and post a reminder.</summary>
internal static class ReminderNotifier
{
    /// <summary>
    /// The channel of the reminders this phone schedules itself. The server's pushes arrive on
    /// channels of their own (see <see cref="AppNotifications"/>); this one keeps its id from before
    /// push existed so whatever the seeker set for it in Android's settings still holds.
    /// </summary>
    internal const string ChannelId = AppNotifications.Reminders;

    internal const string ExtraTitle = "title";
    internal const string ExtraBody = "body";
    internal const string ExtraId = "id";

    /// <summary>The channel exists once <see cref="AppNotifications.EnsureChannels"/> has run; kept so callers read as before.</summary>
    internal static void EnsureChannel(Context context) => AppNotifications.EnsureChannels(context);

    /// <summary>
    /// Posts one reminder, opening the app when tapped. Drawn by <see cref="AppNotifications"/>, the
    /// same renderer a push goes through, so a reminder scheduled on the phone and a notification
    /// sent by the server are indistinguishable in the shade.
    /// </summary>
    internal static void Show(Context context, string id, string title, string? body)
        => AppNotifications.Show(context, new AppNotification(id, title, body, ChannelId));

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
