using Sanathana.Companion.Domain.Entities;
using PanchangamDay = Sanathana.Companion.Domain.Entities.Panchangam;

namespace Sanathana.Companion.Application.Interfaces;

/// <summary>One of a seeker's phones, as far as sending to it is concerned.</summary>
public sealed record AudienceDevice(Guid Id, string Token, int Platform);

/// <summary>A seeker's saved choice for one notification type.</summary>
public sealed record AudiencePreference(bool IsEnabled, TimeOnly? FromTime, TimeOnly? ToTime);

/// <summary>
/// Everything the engine needs to know about one seeker who can be reached: who they are, where
/// and in which language their phone is, and what they have asked for.
/// </summary>
public sealed class AudienceMember
{
    public Guid UserId { get; init; }

    /// <summary>What a notification calls them: the seeker name when there is one, else the first name.</summary>
    public string Name { get; init; } = string.Empty;

    public string? RoleName { get; init; }
    public Guid? RegionId { get; init; }

    /// <summary>The language of the most recently used device. "en" when none was reported.</summary>
    public string Language { get; init; } = "en";

    /// <summary>IANA id from the most recently used device; null reads as India.</summary>
    public string? TimeZoneId { get; init; }

    public bool MasterEnabled { get; init; } = true;
    public bool QuietHoursEnabled { get; init; }
    public TimeOnly? QuietFrom { get; init; }
    public TimeOnly? QuietTo { get; init; }

    public IReadOnlyList<AudienceDevice> Devices { get; init; } = Array.Empty<AudienceDevice>();

    /// <summary>Keyed by NotificationConfig id. No entry means "follow the type's default".</summary>
    public IReadOnlyDictionary<Guid, AudiencePreference> Preferences { get; init; }
        = new Dictionary<Guid, AudiencePreference>();
}

/// <summary>What one device did with one message, for the token bookkeeping.</summary>
/// <param name="TokenDead">The push service says this token will never work again; stop using it.</param>
public sealed record DeviceOutcome(Guid DeviceId, bool Ok, bool TokenDead, string? Error);

/// <summary>A filter over the delivery history, for the console.</summary>
public sealed record NotificationLogQuery(string? Kind, string? Status, string? Search, int Page, int PageSize);

/// <summary>One delivery-history row with the seeker's name beside it.</summary>
public sealed record NotificationLogRow(NotificationLog Log, string? UserName);

/// <summary>Sent and failed counts for one notification type.</summary>
public sealed record KindCount(string Kind, int Sent, int Failed);

/// <summary>Sent and failed counts for one IST day.</summary>
public sealed record DayCount(DateOnly Date, int Sent, int Failed);

/// <summary>One registered device with its owner's name, for the console.</summary>
public sealed record DeviceRow(DeviceToken Device, string? UserName);

/// <summary>
/// The notification engine's reads and writes. One interface rather than a handful of repository
/// methods because every query here exists for the engine or its console and nowhere else, and
/// most of them span tables the repositories keep apart.
/// </summary>
public interface INotificationStore
{
    // ---- who can be reached ----

    /// <summary>
    /// Active seekers with at least one active device, with their settings and preferences. Pass ids
    /// to look at only those seekers (a test send, a targeted message).
    /// </summary>
    Task<IReadOnlyList<AudienceMember>> GetAudienceAsync(IReadOnlyCollection<Guid>? onlyUsers = null, CancellationToken cancellationToken = default);

    /// <summary>Every notification type with its form, untracked.</summary>
    Task<IReadOnlyList<NotificationConfig>> GetConfigsAsync(CancellationToken cancellationToken = default);

    // ---- what the rules read ----

    Task<IReadOnlyDictionary<Guid, SadhanaStreak>> GetStreaksAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PanchangamDay>> GetPanchangamAsync(IReadOnlyCollection<Guid> regionIds, DateOnly from, DateOnly to, CancellationToken cancellationToken = default);

    /// <summary>The region used for a seeker who has not chosen one: the first active region with coordinates.</summary>
    Task<Guid?> GetFallbackRegionIdAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Festival>> GetFestivalsAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken = default);

    /// <summary>The seekers' own calendar marks that fall on a date, yearly repeats included.</summary>
    Task<IReadOnlyList<CalendarEvent>> GetCalendarEventsAsync(IReadOnlyCollection<Guid> userIds, DateOnly date, CancellationToken cancellationToken = default);

    // ---- sending, exactly once ----

    /// <summary>Which of these dedupe keys already have a row.</summary>
    Task<HashSet<string>> GetExistingKeysAsync(IReadOnlyCollection<string> keys, CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes the rows as Pending BEFORE anything is sent and returns the ones that were written. A
    /// row whose key another run already holds is left out, so its notification is not sent again.
    /// </summary>
    Task<IReadOnlyList<NotificationLog>> ClaimAsync(IReadOnlyList<NotificationLog> logs, CancellationToken cancellationToken = default);

    /// <summary>Saves the outcome on the claimed rows and updates every device that was tried.</summary>
    Task CompleteAsync(IReadOnlyList<NotificationLog> logs, IReadOnlyList<DeviceOutcome> outcomes, DateTime nowUtc, CancellationToken cancellationToken = default);

    Task AddRunAsync(NotificationRun run, CancellationToken cancellationToken = default);

    // ---- devices ----

    Task<DeviceToken> UpsertDeviceAsync(Guid userId, int platform, string token, string? deviceModel, string? appVersion, string? timeZoneId, string? language, CancellationToken cancellationToken = default);

    /// <summary>Removes a device by its token, whoever it belongs to. True when there was one.</summary>
    Task<bool> RemoveDeviceAsync(string token, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DeviceRow>> GetDevicesAsync(int take, CancellationToken cancellationToken = default);

    // ---- the seeker's inbox ----

    Task<IReadOnlyList<NotificationLog>> GetInboxAsync(Guid userId, int take, CancellationToken cancellationToken = default);
    Task<int> CountUnreadAsync(Guid userId, CancellationToken cancellationToken = default);
    Task MarkInboxReadAsync(Guid userId, DateTime nowUtc, CancellationToken cancellationToken = default);

    // ---- the console ----

    Task<(IReadOnlyList<NotificationLogRow> Rows, int Total)> GetLogsAsync(NotificationLogQuery query, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NotificationRun>> GetRunsAsync(string? kind, int take, CancellationToken cancellationToken = default);

    /// <summary>The most recent recorded run of each type.</summary>
    Task<IReadOnlyDictionary<string, NotificationRun>> GetLastRunsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<KindCount>> GetKindCountsAsync(DateTime sinceUtc, CancellationToken cancellationToken = default);

    /// <summary>Per IST day, oldest first, from <paramref name="sinceUtc"/> to now.</summary>
    Task<IReadOnlyList<DayCount>> GetDayCountsAsync(DateTime sinceUtc, CancellationToken cancellationToken = default);

    Task<DateTime?> GetLastSentAtAsync(CancellationToken cancellationToken = default);

    /// <summary>Drops history older than the cutoff. Returns how many rows went.</summary>
    Task<int> DeleteHistoryOlderThanAsync(DateTime cutoffUtc, CancellationToken cancellationToken = default);
}
