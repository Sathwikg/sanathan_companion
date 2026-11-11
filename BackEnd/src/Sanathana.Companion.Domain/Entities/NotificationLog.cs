using Sanathana.Companion.Domain.Common;

namespace Sanathana.Companion.Domain.Entities;

/// <summary>
/// One notification addressed to one seeker: what it said, why it was raised and what became of it.
/// </summary>
/// <remarks>
/// Three things read this table. The engine reads <see cref="DedupeKey"/> so nothing is ever sent
/// twice, however often the job runs or restarts. The seeker's bell reads the delivered rows as
/// their inbox. The administrator's console reads all of it as the delivery history.
/// <para>
/// Holds a plain user id rather than a foreign key, like <see cref="UserFavorite"/>; the account
/// purge in UserRepository removes a seeker's rows with the rest of their data.
/// </para>
/// </remarks>
public class NotificationLog : BaseEntity
{
    public const int MaxTitleLength = 200;
    public const int MaxBodyLength = 1000;
    public const int MaxDedupeKeyLength = 200;

    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }

    /// <summary>The notification type's code, kept even if the config row is later removed.</summary>
    public string Kind { get; set; } = string.Empty;

    public Guid? NotificationConfigId { get; set; }

    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;

    /// <summary>The in-app page a tap opens ("/sadhana"). Null opens the home.</summary>
    public string? Route { get; set; }

    /// <summary>
    /// What makes this notification "the same one": type, seeker and the occasion (a date, a window's
    /// start). Unique, so a second attempt to raise it fails in the database instead of reaching the phone.
    /// </summary>
    public string DedupeKey { get; set; } = string.Empty;

    /// <summary>One of <see cref="NotificationStatuses"/>.</summary>
    public string Status { get; set; } = NotificationStatuses.Sent;

    /// <summary>How many of the seeker's devices accepted it.</summary>
    public int Devices { get; set; }

    /// <summary>Why it failed, in the push service's own words. Null when it did not.</summary>
    public string? Error { get; set; }

    /// <summary>One of <see cref="NotificationTriggers"/>: the schedule, an administrator's "run now", a test, or a broadcast.</summary>
    public string Trigger { get; set; } = NotificationTriggers.Schedule;

    public DateTime? SentAtUtc { get; set; }

    /// <summary>When the seeker opened their inbox with this in it. Null is unread.</summary>
    public DateTime? ReadAtUtc { get; set; }
}

/// <summary>What became of a notification. Stored as-is.</summary>
public static class NotificationStatuses
{
    /// <summary>
    /// The row has been written and the send has not finished. A row left like this means the
    /// process stopped in between; it is never retried, because sending late is worse than not at all.
    /// </summary>
    public const string Pending = "Pending";

    /// <summary>At least one device accepted it.</summary>
    public const string Sent = "Sent";

    /// <summary>Every device refused it; <see cref="NotificationLog.Error"/> says why.</summary>
    public const string Failed = "Failed";

    public static readonly IReadOnlyList<string> All = new[] { Sent, Failed, Pending };
}

/// <summary>What caused a notification or an engine run. Stored as-is.</summary>
public static class NotificationTriggers
{
    public const string Schedule = "Schedule";
    public const string Manual = "Manual";
    public const string Test = "Test";
    public const string Broadcast = "Broadcast";
}
