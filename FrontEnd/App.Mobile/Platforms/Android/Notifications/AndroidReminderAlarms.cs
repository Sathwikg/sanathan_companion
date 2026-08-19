using System.Text.Json;
using Android.App;
using Android.Content;
using Microsoft.Maui.Storage;

namespace App.Mobile.Platforms.Android.Notifications;

/// <summary>
/// Holds the daily reminder alarms, and the on-disk copy of the plan used to rebuild them.
/// </summary>
/// <remarks>
/// Alarms are <b>inexact</b> on purpose. SetInexactRepeating lets Android batch the wake-up with
/// others it was already going to perform, which is the single biggest battery difference available
/// here; an exact alarm forces a dedicated wake and, from Android 12, needs the
/// SCHEDULE_EXACT_ALARM permission that Play reviews. A devotional reminder that arrives within a
/// few minutes of the hour is entirely fine — this is a nudge, not an alarm clock.
/// </remarks>
internal static class AndroidReminderAlarms
{
    /// <summary>The last synced plan, so a reboot can rebuild without a network call or a session.</summary>
    private const string CacheKey = "sc-reminders";

    internal sealed record CachedReminder(string Id, string Title, string? Body, int Hour, int Minute);

    internal static void Sync(Context context, IReadOnlyList<CachedReminder> reminders)
    {
        CancelAll(context);

        foreach (var reminder in reminders) Schedule(context, reminder);

        Preferences.Default.Set(CacheKey, JsonSerializer.Serialize(reminders));
    }

    internal static void CancelAll(Context context)
    {
        foreach (var reminder in ReadCache())
        {
            var pending = BuildPendingIntent(context, reminder, PendingIntentFlags.NoCreate);
            if (pending is null) continue;

            Alarms(context)?.Cancel(pending);
            pending.Cancel();
        }

        Preferences.Default.Remove(CacheKey);
    }

    internal static void RescheduleFromCache(Context context)
    {
        foreach (var reminder in ReadCache()) Schedule(context, reminder);
    }

    private static void Schedule(Context context, CachedReminder reminder)
    {
        var manager = Alarms(context);
        var pending = BuildPendingIntent(context, reminder, PendingIntentFlags.UpdateCurrent);
        if (manager is null || pending is null) return;

        manager.SetInexactRepeating(
            AlarmType.RtcWakeup,
            NextOccurrenceMillis(reminder.Hour, reminder.Minute),
            AlarmManager.IntervalDay,
            pending);
    }

    /// <summary>
    /// The next time this wall-clock hour comes round, in device-local time — today if it is still
    /// ahead, otherwise tomorrow. Wall clock rather than a fixed instant is the point: 06:00 stays
    /// 06:00 across a timezone change or daylight saving, which is what a daily routine means.
    /// </summary>
    private static long NextOccurrenceMillis(int hour, int minute)
    {
        var now = DateTime.Now;
        var next = new DateTime(now.Year, now.Month, now.Day, hour, minute, 0, DateTimeKind.Local);
        if (next <= now) next = next.AddDays(1);

        return (long)(next.ToUniversalTime() - DateTime.UnixEpoch).TotalMilliseconds;
    }

    private static PendingIntent? BuildPendingIntent(Context context, CachedReminder reminder, PendingIntentFlags flags)
    {
        var intent = new Intent(context, typeof(ReminderReceiver))
            .PutExtra(ReminderNotifier.ExtraId, reminder.Id)
            .PutExtra(ReminderNotifier.ExtraTitle, reminder.Title)
            .PutExtra(ReminderNotifier.ExtraBody, reminder.Body);

        return PendingIntent.GetBroadcast(
            context,
            ReminderNotifier.RequestCode(reminder.Id),
            intent,
            flags | PendingIntentFlags.Immutable);
    }

    private static AlarmManager? Alarms(Context context)
        => (AlarmManager?)context.GetSystemService(Context.AlarmService);

    private static IReadOnlyList<CachedReminder> ReadCache()
    {
        try
        {
            var json = Preferences.Default.Get(CacheKey, string.Empty);
            if (string.IsNullOrWhiteSpace(json)) return [];

            return JsonSerializer.Deserialize<List<CachedReminder>>(json) ?? [];
        }
        catch
        {
            return []; // a corrupt cache just means nothing to rebuild
        }
    }
}
