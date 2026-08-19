using Android.App;
using Android.Content;

namespace App.Mobile.Platforms.Android.Notifications;

/// <summary>
/// Woken by the AlarmManager at a reminder's time, and posts it.
/// </summary>
/// <remarks>
/// Not exported: only this app's own alarms may trigger it, so another app cannot make the phone
/// show an arbitrary notification with our identity.
/// </remarks>
[BroadcastReceiver(Enabled = true, Exported = false)]
public class ReminderReceiver : BroadcastReceiver
{
    public override void OnReceive(Context? context, Intent? intent)
    {
        if (context is null || intent is null) return;

        var id = intent.GetStringExtra(ReminderNotifier.ExtraId);
        var title = intent.GetStringExtra(ReminderNotifier.ExtraTitle);
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(title)) return;

        ReminderNotifier.Show(context, id!, title!, intent.GetStringExtra(ReminderNotifier.ExtraBody));
    }
}
