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

    public static IReadOnlyList<Reminder> Plan(MyNotificationSettings? settings)
    {
        if (settings is null) return Array.Empty<Reminder>();

        var reminders = new List<Reminder>();

        foreach (var item in settings.Items)
        {
            // A mandatory type ignores the master switch and an opt-out, exactly as the server's
            // own Resolve() does — the two must not disagree about what is on.
            var enabled = item.IsMandatory || (item.IsEnabled && settings.MasterEnabled);
            if (!enabled) continue;

            // No window means "any time of day", which is a permission, not a schedule. Firing at
            // some invented hour would be worse than staying silent.
            if (item.FromTime is not { } start) continue;

            // Quiet hours outrank everything, mandatory included. The server takes the same line:
            // quiet hours exist so the app stays silent while the seeker rests.
            if (settings.QuietHoursEnabled && Contains(settings.QuietFrom, settings.QuietTo, start))
                continue;

            reminders.Add(new Reminder(
                item.ConfigId,
                string.IsNullOrWhiteSpace(item.Title) ? item.ModuleName : item.Title,
                string.IsNullOrWhiteSpace(item.Description) ? null : item.Description,
                start.Hour,
                start.Minute));
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
