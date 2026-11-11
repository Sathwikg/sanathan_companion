using App.Core.Models;

namespace App.Core.Services;

/// <summary>
/// Turns the seeker's notification preferences into a set of daily reminders a phone can schedule.
/// </summary>
/// <remarks>
/// Deliberately pure and platform-free so the rules can be tested without a device.
/// <para>
/// Two decisions are encoded here that the data model does not state outright:
/// </para>
/// <list type="number">
/// <item>
/// <b>The window's start is the reminder time.</b> The model stores <c>FromTime</c>/<c>ToTime</c>
/// as a permission window — <c>TimeWindow.Contains</c> only ever answers "is it inside right now?"
/// — so there is no instant to fire at. Reading "06:00–07:00" as "remind me at 06:00" is the
/// interpretation a seeker expects, and it means an item with no window schedules nothing rather
/// than firing at an arbitrary hour.
/// </item>
/// <item>
/// <b>The times are device-local wall clock.</b> The server computes in IST throughout, but a
/// sadhana reminder is part of a personal daily routine: 06:00 should mean 06:00 wherever the
/// seeker is, which is also the only reading that works for anyone outside India. The stored value
/// is therefore treated as a wall-clock time, never converted to an instant.
/// </item>
/// <item>
/// <b>The server sends what it can; the phone schedules the rest.</b> Since push arrived, most
/// notification types are raised by the server and never appear here. Two kinds still can: a plain
/// reminder ("Device"), which only this phone ever schedules, and a type the seeker times
/// themselves ("UserTime", the daily sadhana reminder), which the server sends when it can reach
/// this phone and which is scheduled here when it cannot — push not set up on the server, no
/// Google Play services, a build without the Firebase file. Never both: a reminder that arrives
/// twice is worse than one that arrives plainly.
/// </item>
/// </list>
/// </remarks>
public static class ReminderPlanner
{
    /// <summary>One daily reminder, at a wall-clock time on the device.</summary>
    /// <param name="ConfigId">The notification type, so a reminder can be cancelled or replaced.</param>
    public sealed record Reminder(Guid ConfigId, string Title, string? Body, int Hour, int Minute)
    {
        /// <summary>Stable per type, so re-syncing replaces a reminder instead of duplicating it.</summary>
        public string Id => ConfigId.ToString("N");
    }

    /// <summary>
    /// A non-negative integer key for a reminder id, identical in every process.
    /// </summary>
    /// <remarks>
    /// Android identifies both a PendingIntent and a posted notification by int. It must be derived
    /// the same way on every run or an alarm scheduled today cannot be cancelled tomorrow.
    /// <c>string.GetHashCode()</c> is explicitly NOT that: .NET randomises string hashing per
    /// process, so the code would differ on each launch, PendingIntent lookups with NoCreate would
    /// miss, and every start would leave the old alarm running and add another — a seeker would
    /// collect a duplicate daily reminder per app launch. FNV-1a is deterministic and adequate:
    /// the ids are GUIDs, so collisions are not a practical concern.
    /// </remarks>
    public static int StableCode(string id)
    {
        unchecked
        {
            const uint offset = 2166136261;
            const uint prime = 16777619;

            var hash = offset;
            foreach (var c in id)
            {
                hash ^= c;
                hash *= prime;
            }

            return (int)(hash & 0x7FFFFFFF);
        }
    }

    /// <summary>The schedule value of a plain reminder this phone alone delivers.</summary>
    public const string DeviceSchedule = "Device";

    /// <summary>The schedule value of a type the seeker times themselves.</summary>
    public const string UserTimeSchedule = "UserTime";

    /// <param name="pushDelivers">
    /// True when the server can push and this phone is registered for it, so the server sends the
    /// types the seeker times themselves and the phone must not schedule them as well.
    /// </param>
    /// <param name="label">
    /// Looks up a label in the seeker's language, or returns null when there is none. Used for the
    /// wording of a built-in type scheduled here, so the fallback reads like the push it stands in for.
    /// </param>
    public static IReadOnlyList<Reminder> Plan(MyNotificationSettings? settings, bool pushDelivers = false, Func<string, string?>? label = null)
    {
        if (settings is null) return Array.Empty<Reminder>();

        var reminders = new List<Reminder>();

        foreach (var item in settings.Items)
        {
            // A mandatory type ignores the master switch and an opt-out, exactly as the server's
            // own Resolve() does — the two must not disagree about what is on.
            var enabled = item.IsMandatory || (item.IsEnabled && settings.MasterEnabled);
            if (!enabled) continue;

            var userTimed = item.Schedule == UserTimeSchedule;

            // Everything else is the server's to send: a Panchangam warning or a streak notice
            // depends on data this phone does not hold. An empty schedule is an older server,
            // which only ever had plain reminders.
            if (!userTimed && !string.IsNullOrEmpty(item.Schedule) && item.Schedule != DeviceSchedule) continue;
            if (userTimed && pushDelivers) continue;

            // No window means "any time of day", which is a permission, not a schedule. Firing at
            // some invented hour would be worse than staying silent. A type the seeker times
            // themselves does have an hour before they choose one: the administrator's default.
            if ((item.FromTime ?? (userTimed ? item.DefaultTime : null)) is not { } start) continue;

            // Quiet hours outrank everything, mandatory included. The server takes the same line:
            // quiet hours exist so the app stays silent while the seeker rests.
            if (settings.QuietHoursEnabled && Contains(settings.QuietFrom, settings.QuietTo, start))
                continue;

            var title = string.IsNullOrWhiteSpace(item.Title) ? item.ModuleName : item.Title;
            var body = string.IsNullOrWhiteSpace(item.Description) ? null : item.Description;

            // A built-in type's description explains the setting ("A gentle call to your japa at
            // the time you choose…"), which is not what a notification should say. Use the push's
            // own wording, in the seeker's language, when it can be found.
            if (userTimed && label is not null)
            {
                title = label($"push.{item.Kind}.title") ?? title;
                body = label($"push.{item.Kind}.body") ?? body;
            }

            reminders.Add(new Reminder(item.ConfigId, title, body, start.Hour, start.Minute));
        }

        return reminders;
    }

    /// <summary>
    /// Wrap-aware window test, matching <c>TimeWindow.Contains</c> on the server so a window such
    /// as 22:00–06:00 means the night, not the empty set. An incomplete window contains nothing.
    /// </summary>
    private static bool Contains(TimeOnly? from, TimeOnly? to, TimeOnly moment)
    {
        if (from is not { } f || to is not { } t) return false;
        return f <= t ? moment >= f && moment <= t : moment >= f || moment <= t;
    }
}
