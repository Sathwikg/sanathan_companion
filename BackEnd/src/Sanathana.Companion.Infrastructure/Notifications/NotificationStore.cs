using Microsoft.EntityFrameworkCore;
using Sanathana.Companion.Application.Interfaces;
using Sanathana.Companion.Domain.Entities;
using Sanathana.Companion.Infrastructure.Persistence;

namespace Sanathana.Companion.Infrastructure.Notifications;

/// <inheritdoc />
public sealed class NotificationStore : INotificationStore
{
    private static readonly TimeSpan IstOffset = TimeSpan.FromHours(5.5);

    private readonly ApplicationDbContext _db;

    public NotificationStore(ApplicationDbContext db) => _db = db;

    // ------------------------------------------------------------------ who can be reached

    public async Task<IReadOnlyList<AudienceMember>> GetAudienceAsync(IReadOnlyCollection<Guid>? onlyUsers = null, CancellationToken cancellationToken = default)
    {
        var devicesQuery = _db.DeviceTokens.AsNoTracking().Where(d => d.IsActive);
        if (onlyUsers is not null)
        {
            var ids = onlyUsers.Distinct().ToList();
            devicesQuery = devicesQuery.Where(d => ids.Contains(d.UserId));
        }

        var devices = await devicesQuery
            .Select(d => new { d.Id, d.UserId, d.Token, d.Platform, d.TimeZoneId, d.Language, Seen = d.LastUsedAt ?? d.ModifiedDate ?? d.CreatedDate })
            .ToListAsync(cancellationToken);

        if (devices.Count == 0) return Array.Empty<AudienceMember>();

        var userIds = devices.Select(d => d.UserId).Distinct().ToList();

        var users = await _db.Users.AsNoTracking()
            .Where(u => u.IsActive && userIds.Contains(u.UserId))
            .Select(u => new { u.UserId, u.FullName, u.SeekerName, u.DefaultRegionId, RoleName = u.Role.RoleName })
            .ToListAsync(cancellationToken);

        var settings = (await _db.UserNotificationSettings.AsNoTracking()
            .Where(s => userIds.Contains(s.UserId))
            .ToListAsync(cancellationToken))
            .GroupBy(s => s.UserId).ToDictionary(g => g.Key, g => g.First());

        var preferences = (await _db.UserNotificationPreferences.AsNoTracking()
            .Where(p => userIds.Contains(p.UserId))
            .ToListAsync(cancellationToken))
            .ToLookup(p => p.UserId);

        var devicesByUser = devices.ToLookup(d => d.UserId);

        var audience = new List<AudienceMember>(users.Count);
        foreach (var user in users)
        {
            // The phone used last speaks for the seeker: its language and its time zone.
            var mine = devicesByUser[user.UserId].OrderByDescending(d => d.Seen).ToList();
            var latest = mine[0];
            settings.TryGetValue(user.UserId, out var setting);

            audience.Add(new AudienceMember
            {
                UserId = user.UserId,
                Name = DisplayName(user.SeekerName, user.FullName),
                RoleName = user.RoleName,
                RegionId = user.DefaultRegionId,
                Language = string.IsNullOrWhiteSpace(latest.Language) ? "en" : latest.Language!.Trim().ToLowerInvariant(),
                TimeZoneId = latest.TimeZoneId,
                MasterEnabled = setting?.MasterEnabled ?? true,
                QuietHoursEnabled = setting?.QuietHoursEnabled ?? false,
                QuietFrom = setting?.QuietFrom,
                QuietTo = setting?.QuietTo,
                Devices = mine.Select(d => new AudienceDevice(d.Id, d.Token, d.Platform)).ToList(),
                Preferences = preferences[user.UserId]
                    .GroupBy(p => p.NotificationConfigId)
                    .ToDictionary(g => g.Key, g => new AudiencePreference(g.First().IsEnabled, g.First().FromTime, g.First().ToTime))
            });
        }

        return audience;
    }

    /// <summary>The seeker name when there is one, else the first word of the full name.</summary>
    private static string DisplayName(string? seekerName, string fullName)
    {
        if (!string.IsNullOrWhiteSpace(seekerName)) return seekerName.Trim();
        var first = (fullName ?? string.Empty).Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return first ?? string.Empty;
    }

    public async Task<IReadOnlyList<NotificationConfig>> GetConfigsAsync(CancellationToken cancellationToken = default)
        => await _db.NotificationConfigs.AsNoTracking()
            .Include(c => c.MenuModule)
            .OrderBy(c => c.DisplayOrder)
            .ToListAsync(cancellationToken);

    // ------------------------------------------------------------------ what the rules read

    public async Task<IReadOnlyDictionary<Guid, SadhanaStreak>> GetStreaksAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken = default)
    {
        var ids = userIds.Distinct().ToList();
        var rows = await _db.SadhanaStreaks.AsNoTracking().Where(s => ids.Contains(s.UserId)).ToListAsync(cancellationToken);
        return rows.GroupBy(s => s.UserId).ToDictionary(g => g.Key, g => g.First());
    }

    public async Task<IReadOnlyList<Panchangam>> GetPanchangamAsync(IReadOnlyCollection<Guid> regionIds, DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        var ids = regionIds.Distinct().ToList();
        return await _db.Panchangams.AsNoTracking()
            .Where(p => ids.Contains(p.RegionId) && p.Date >= from && p.Date <= to)
            .ToListAsync(cancellationToken);
    }

    public async Task<Guid?> GetFallbackRegionIdAsync(CancellationToken cancellationToken = default)
    {
        var region = await _db.Regions.AsNoTracking()
            .Where(r => r.IsActive && r.Latitude != null && r.Longitude != null)
            .OrderBy(r => r.Name)
            .Select(r => (Guid?)r.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return region;
    }

    public async Task<IReadOnlyList<Festival>> GetFestivalsAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
        => await _db.Festivals.AsNoTracking()
            .Where(f => f.Date >= from && f.Date <= to)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<CalendarEvent>> GetCalendarEventsAsync(IReadOnlyCollection<Guid> userIds, DateOnly date, CancellationToken cancellationToken = default)
    {
        var ids = userIds.Distinct().ToList();

        // Narrowed in the database to the seekers and to "this date or any yearly repeat"; the
        // month-and-day match is done here because a yearly repeat is rare and the set is small.
        var rows = await _db.CalendarEvents.AsNoTracking()
            .Where(e => ids.Contains(e.UserId) && (e.Date == date || (e.RepeatYearly && e.Date < date)))
            .ToListAsync(cancellationToken);

        return rows.Where(e => e.Date == date || (e.Date.Month == date.Month && e.Date.Day == date.Day)).ToList();
    }

    // ------------------------------------------------------------------ sending, exactly once

    public async Task<HashSet<string>> GetExistingKeysAsync(IReadOnlyCollection<string> keys, CancellationToken cancellationToken = default)
    {
        var list = keys.Distinct().ToList();
        if (list.Count == 0) return new HashSet<string>(StringComparer.Ordinal);

        var found = await _db.NotificationLogs.AsNoTracking()
            .Where(l => list.Contains(l.DedupeKey))
            .Select(l => l.DedupeKey)
            .ToListAsync(cancellationToken);

        return found.ToHashSet(StringComparer.Ordinal);
    }

    public async Task<IReadOnlyList<NotificationLog>> ClaimAsync(IReadOnlyList<NotificationLog> logs, CancellationToken cancellationToken = default)
    {
        if (logs.Count == 0) return logs;

        _db.NotificationLogs.AddRange(logs);
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
            return logs;
        }
        catch (DbUpdateException)
        {
            // Another run wrote at least one of these keys between our check and our insert. Drop
            // the batch from the change tracker and take the rows one at a time: the ones that are
            // genuinely ours go in, the rest are refused by the unique index and left out.
            foreach (var log in logs) _db.Entry(log).State = EntityState.Detached;
        }

        var claimed = new List<NotificationLog>();
        foreach (var log in logs)
        {
            _db.NotificationLogs.Add(log);
            try
            {
                await _db.SaveChangesAsync(cancellationToken);
                claimed.Add(log);
            }
            catch (DbUpdateException)
            {
                _db.Entry(log).State = EntityState.Detached;
            }
        }

        return claimed;
    }

    public async Task CompleteAsync(IReadOnlyList<NotificationLog> logs, IReadOnlyList<DeviceOutcome> outcomes, DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        // The log rows are still tracked from ClaimAsync, so their new status saves with the rest.
        if (outcomes.Count > 0)
        {
            var ids = outcomes.Select(o => o.DeviceId).Distinct().ToList();
            var devices = await _db.DeviceTokens.Where(d => ids.Contains(d.Id)).ToDictionaryAsync(d => d.Id, cancellationToken);

            foreach (var outcome in outcomes)
            {
                if (!devices.TryGetValue(outcome.DeviceId, out var device)) continue;

                if (outcome.Ok)
                {
                    device.LastUsedAt = nowUtc;
                    device.FailureCount = 0;
                    device.LastFailureReason = null;
                }
                else
                {
                    device.FailureCount++;
                    device.LastFailureReason = outcome.Error is { Length: > 500 } e ? e[..500] : outcome.Error;

                    // A token the push service has disowned is finished; anything else (a timeout,
                    // a quota) may work next time and is left alone.
                    if (outcome.TokenDead) device.IsActive = false;
                }
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task AddRunAsync(NotificationRun run, CancellationToken cancellationToken = default)
    {
        _db.NotificationRuns.Add(run);
        await _db.SaveChangesAsync(cancellationToken);
    }

    // ------------------------------------------------------------------ devices

    public async Task<DeviceToken> UpsertDeviceAsync(Guid userId, int platform, string token, string? deviceModel, string? appVersion, string? timeZoneId, string? language, CancellationToken cancellationToken = default)
    {
        var device = await _db.DeviceTokens.FirstOrDefaultAsync(d => d.Token == token, cancellationToken);
        if (device is null)
        {
            device = new DeviceToken { Token = token };
            _db.DeviceTokens.Add(device);
        }

        // A token is one installation of the app. Whoever is signed in on it now owns it: the
        // previous seeker's notifications must not follow the phone to the next one.
        device.UserId = userId;
        device.Platform = platform;
        device.DeviceModel = Cut(deviceModel, 100);
        device.AppVersion = Cut(appVersion, 40);
        device.TimeZoneId = Cut(timeZoneId, 64);
        device.Language = Cut(language, 10)?.ToLowerInvariant();
        device.IsActive = true;
        device.FailureCount = 0;
        device.LastFailureReason = null;

        await _db.SaveChangesAsync(cancellationToken);
        return device;
    }

    public async Task<bool> RemoveDeviceAsync(string token, CancellationToken cancellationToken = default)
    {
        var device = await _db.DeviceTokens.FirstOrDefaultAsync(d => d.Token == token, cancellationToken);
        if (device is null) return false;

        _db.DeviceTokens.Remove(device);
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<DeviceRow>> GetDevicesAsync(int take, CancellationToken cancellationToken = default)
    {
        var devices = await _db.DeviceTokens.AsNoTracking()
            .OrderByDescending(d => d.LastUsedAt ?? d.ModifiedDate ?? d.CreatedDate)
            .Take(Math.Clamp(take, 1, 500))
            .ToListAsync(cancellationToken);

        var names = await NamesAsync(devices.Select(d => d.UserId), cancellationToken);
        return devices.Select(d => new DeviceRow(d, names.GetValueOrDefault(d.UserId))).ToList();
    }

    private async Task<Dictionary<Guid, string>> NamesAsync(IEnumerable<Guid> userIds, CancellationToken cancellationToken)
    {
        var ids = userIds.Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<Guid, string>();

        return await _db.Users.AsNoTracking()
            .Where(u => ids.Contains(u.UserId))
            .ToDictionaryAsync(u => u.UserId, u => u.FullName, cancellationToken);
    }

    // ------------------------------------------------------------------ the seeker's inbox

    public async Task<IReadOnlyList<NotificationLog>> GetInboxAsync(Guid userId, int take, CancellationToken cancellationToken = default)
        => await _db.NotificationLogs.AsNoTracking()
            .Where(l => l.UserId == userId && l.Status == NotificationStatuses.Sent)
            .OrderByDescending(l => l.CreatedDate)
            .Take(Math.Clamp(take, 1, 100))
            .ToListAsync(cancellationToken);

    public Task<int> CountUnreadAsync(Guid userId, CancellationToken cancellationToken = default)
        => _db.NotificationLogs.CountAsync(l => l.UserId == userId && l.Status == NotificationStatuses.Sent && l.ReadAtUtc == null, cancellationToken);

    public async Task MarkInboxReadAsync(Guid userId, DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        var unread = await _db.NotificationLogs
            .Where(l => l.UserId == userId && l.Status == NotificationStatuses.Sent && l.ReadAtUtc == null)
            .ToListAsync(cancellationToken);

        if (unread.Count == 0) return;

        foreach (var log in unread) log.ReadAtUtc = nowUtc;
        await _db.SaveChangesAsync(cancellationToken);
    }

    // ------------------------------------------------------------------ the console

    public async Task<(IReadOnlyList<NotificationLogRow> Rows, int Total)> GetLogsAsync(NotificationLogQuery query, CancellationToken cancellationToken = default)
    {
        var logs = _db.NotificationLogs.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Kind)) logs = logs.Where(l => l.Kind == query.Kind);
        if (!string.IsNullOrWhiteSpace(query.Status)) logs = logs.Where(l => l.Status == query.Status);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = SqlLike.Contains(query.Search);
            var matchingUsers = _db.Users.Where(u => EF.Functions.ILike(u.FullName, pattern, SqlLike.EscapeChar)).Select(u => u.UserId);
            logs = logs.Where(l => EF.Functions.ILike(l.Title, pattern, SqlLike.EscapeChar)
                                || EF.Functions.ILike(l.Body, pattern, SqlLike.EscapeChar)
                                || matchingUsers.Contains(l.UserId));
        }

        var total = await logs.CountAsync(cancellationToken);

        var pageSize = Math.Clamp(query.PageSize, 1, 200);
        var page = Math.Max(1, query.Page);
        var rows = await logs
            .OrderByDescending(l => l.CreatedDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var names = await NamesAsync(rows.Select(r => r.UserId), cancellationToken);
        return (rows.Select(r => new NotificationLogRow(r, names.GetValueOrDefault(r.UserId))).ToList(), total);
    }

    public async Task<IReadOnlyList<NotificationRun>> GetRunsAsync(string? kind, int take, CancellationToken cancellationToken = default)
    {
        var runs = _db.NotificationRuns.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(kind)) runs = runs.Where(r => r.Kind == kind);

        return await runs.OrderByDescending(r => r.StartedAtUtc).Take(Math.Clamp(take, 1, 200)).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<string, NotificationRun>> GetLastRunsAsync(CancellationToken cancellationToken = default)
    {
        // The history is pruned to a couple of months and only non-empty passes are kept, so the
        // recent slice is small; the newest per type is picked from it in memory.
        var recent = await _db.NotificationRuns.AsNoTracking()
            .OrderByDescending(r => r.StartedAtUtc)
            .Take(2000)
            .ToListAsync(cancellationToken);

        return recent.GroupBy(r => r.Kind).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
    }

    public async Task<IReadOnlyList<KindCount>> GetKindCountsAsync(DateTime sinceUtc, CancellationToken cancellationToken = default)
    {
        var rows = await _db.NotificationLogs.AsNoTracking()
            .Where(l => l.CreatedDate >= sinceUtc)
            .GroupBy(l => new { l.Kind, l.Status })
            .Select(g => new { g.Key.Kind, g.Key.Status, Count = g.Count() })
            .ToListAsync(cancellationToken);

        return rows.GroupBy(r => r.Kind)
            .Select(g => new KindCount(g.Key,
                g.Where(r => r.Status == NotificationStatuses.Sent).Sum(r => r.Count),
                g.Where(r => r.Status == NotificationStatuses.Failed).Sum(r => r.Count)))
            .ToList();
    }

    public async Task<IReadOnlyList<DayCount>> GetDayCountsAsync(DateTime sinceUtc, CancellationToken cancellationToken = default)
    {
        // Grouped by India's day, which a UTC timestamp cannot be grouped by in SQL without tying the
        // query to one database's time zone functions. A fortnight of rows is cheap to bucket here.
        var rows = await _db.NotificationLogs.AsNoTracking()
            .Where(l => l.CreatedDate >= sinceUtc)
            .Select(l => new { l.CreatedDate, l.Status })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(r => DateOnly.FromDateTime(r.CreatedDate + IstOffset))
            .OrderBy(g => g.Key)
            .Select(g => new DayCount(g.Key,
                g.Count(r => r.Status == NotificationStatuses.Sent),
                g.Count(r => r.Status == NotificationStatuses.Failed)))
            .ToList();
    }

    public async Task<DateTime?> GetLastSentAtAsync(CancellationToken cancellationToken = default)
        => await _db.NotificationLogs.AsNoTracking()
            .Where(l => l.Status == NotificationStatuses.Sent)
            .OrderByDescending(l => l.CreatedDate)
            .Select(l => l.SentAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<int> DeleteHistoryOlderThanAsync(DateTime cutoffUtc, CancellationToken cancellationToken = default)
    {
        var logs = await _db.NotificationLogs.Where(l => l.CreatedDate < cutoffUtc).ExecuteDeleteAsync(cancellationToken);
        var runs = await _db.NotificationRuns.Where(r => r.StartedAtUtc < cutoffUtc).ExecuteDeleteAsync(cancellationToken);
        return logs + runs;
    }

    private static string? Cut(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        return trimmed.Length <= max ? trimmed : trimmed[..max];
    }
}
