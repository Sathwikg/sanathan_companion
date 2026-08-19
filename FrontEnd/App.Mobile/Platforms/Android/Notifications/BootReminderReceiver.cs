using Android.App;
using Android.Content;

namespace App.Mobile.Platforms.Android.Notifications;

/// <summary>
/// Rebuilds the daily alarms after a restart.
/// </summary>
/// <remarks>
/// Android drops every pending alarm when the device reboots, so without this a seeker's reminders
/// would stop the first time they restarted their phone and never come back — the most likely way
/// for this feature to fail silently in the field.
/// <para>
/// It re-schedules from the plan cached on disk rather than from the API: there is no network
/// guarantee at boot, and no signed-in session to call with.
/// </para>
/// </remarks>
[BroadcastReceiver(Enabled = true, Exported = true, Permission = "android.permission.RECEIVE_BOOT_COMPLETED")]
[IntentFilter([Intent.ActionBootCompleted, "android.intent.action.QUICKBOOT_POWERON"])]
public class BootReminderReceiver : BroadcastReceiver
{
    public override void OnReceive(Context? context, Intent? intent)
    {
        if (context is null) return;

        var action = intent?.Action;
        if (action != Intent.ActionBootCompleted && action != "android.intent.action.QUICKBOOT_POWERON")
            return;

        try { AndroidReminderAlarms.RescheduleFromCache(context); }
        catch (System.Exception) { /* a failed reschedule must not crash the boot broadcast */ }
    }
}
