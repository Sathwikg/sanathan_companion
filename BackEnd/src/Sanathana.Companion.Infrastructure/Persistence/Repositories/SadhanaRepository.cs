using Microsoft.EntityFrameworkCore;
using Sanathana.Companion.Domain.Entities;
using Sanathana.Companion.Domain.Interfaces;

namespace Sanathana.Companion.Infrastructure.Persistence.Repositories;

public class SadhanaRepository : BaseRepository<SadhanaLog>, ISadhanaRepository
{
    public SadhanaRepository(ApplicationDbContext context) : base(context) { }

    public async Task<SadhanaTotals> GetTotalsAsync(DateOnly today, CancellationToken cancellationToken = default)
    {
        var streaks = Context.Set<SadhanaStreak>().AsNoTracking();

        // Nullable projections so SUM/MAX over an empty table yield null → coalesce to 0
        // (a non-nullable projection would throw when no rows exist).
        var totalMalas = await streaks.SumAsync(s => (int?)s.TotalMalas, cancellationToken) ?? 0;
        var totalDays = await streaks.SumAsync(s => (int?)s.TotalDaysPracticed, cancellationToken) ?? 0;
        var longestStreak = await streaks.MaxAsync(s => (int?)s.LongestStreak, cancellationToken) ?? 0;
        var activeToday = await streaks.CountAsync(s => s.LastPracticeDate == today, cancellationToken);

        var totalJapa = await Set.AsNoTracking().SumAsync(s => (long?)s.TotalCount, cancellationToken) ?? 0L;
        var totalSessions = await Set.CountAsync(cancellationToken);

        return new SadhanaTotals(totalMalas, totalDays, longestStreak, activeToday, totalJapa, totalSessions);
    }

    public async Task<SadhanaLog?> GetLogAsync(Guid userId, DateOnly date, Guid chantConfigId, CancellationToken cancellationToken = default)
        => await Set.FirstOrDefaultAsync(x => x.UserId == userId && x.Date == date && x.ChantConfigId == chantConfigId, cancellationToken);

    public async Task<IReadOnlyList<SadhanaLog>> GetLogsForDateAsync(Guid userId, DateOnly date, CancellationToken cancellationToken = default)
        => await Set.AsNoTracking().Where(x => x.UserId == userId && x.Date == date)
            .OrderByDescending(x => x.ModifiedDate ?? x.CreatedDate).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<SadhanaLog>> GetHistoryAsync(Guid userId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
        => await Set.AsNoTracking().Where(x => x.UserId == userId && x.Date >= from && x.Date <= to)
            .OrderByDescending(x => x.Date).ToListAsync(cancellationToken);

    // Both totals skip rows still at zero: the japa screen's Reset can save a log with nothing
    // counted, and that is not a day of practice.
    public async Task<IReadOnlyList<SadhanaDayTotal>> GetDailyTotalsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var rows = await Set.AsNoTracking()
            .Where(x => x.UserId == userId && x.TotalCount > 0)
            .GroupBy(x => x.Date)
            .Select(g => new
            {
                Date = g.Key,
                Malas = g.Sum(x => x.MalasCompleted),
                Japa = g.Sum(x => (long)x.TotalCount),
                Chants = g.Count()
            })
            .OrderBy(x => x.Date)
            .ToListAsync(cancellationToken);

        return rows.Select(r => new SadhanaDayTotal(r.Date, r.Malas, r.Japa, r.Chants)).ToList();
    }

    public async Task<IReadOnlyList<SadhanaChantTotal>> GetChantTotalsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        // Grouped on the chant, not its name: the name is denormalised onto each log, so a chant
        // renamed mid-history would otherwise split in two. MAX picks one spelling for the label.
        var rows = await Set.AsNoTracking()
            .Where(x => x.UserId == userId && x.TotalCount > 0)
            .GroupBy(x => x.ChantConfigId)
            .Select(g => new
            {
                ChantConfigId = g.Key,
                ChantName = g.Max(x => x.ChantName),
                DeityName = g.Max(x => x.DeityName),
                Malas = g.Sum(x => x.MalasCompleted),
                Japa = g.Sum(x => (long)x.TotalCount),
                Days = g.Count(),
                LastDate = g.Max(x => x.Date)
            })
            .ToListAsync(cancellationToken);

        return rows.Select(r => new SadhanaChantTotal(
            r.ChantConfigId, r.ChantName ?? string.Empty, r.DeityName, r.Malas, r.Japa, r.Days, r.LastDate)).ToList();
    }

    public async Task<SadhanaStreak?> GetStreakAsync(Guid userId, CancellationToken cancellationToken = default)
        => await Context.Set<SadhanaStreak>().FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);

    public async Task<IReadOnlyList<SadhanaStreak>> GetStreaksAsync(IEnumerable<Guid> userIds, CancellationToken cancellationToken = default)
    {
        var ids = userIds.ToList();
        return await Context.Set<SadhanaStreak>().AsNoTracking()
            .Where(x => ids.Contains(x.UserId)).ToListAsync(cancellationToken);
    }

    public async Task AddStreakAsync(SadhanaStreak streak, CancellationToken cancellationToken = default)
        => await Context.Set<SadhanaStreak>().AddAsync(streak, cancellationToken);

    public async Task<UserChantSetting?> GetChantSettingAsync(Guid userId, Guid chantConfigId, CancellationToken cancellationToken = default)
        => await Context.Set<UserChantSetting>()
            .FirstOrDefaultAsync(x => x.UserId == userId && x.ChantConfigId == chantConfigId, cancellationToken);

    public async Task<IReadOnlyList<UserChantSetting>> GetChantSettingsAsync(Guid userId, CancellationToken cancellationToken = default)
        => await Context.Set<UserChantSetting>().AsNoTracking()
            .Where(x => x.UserId == userId).ToListAsync(cancellationToken);

    public async Task AddChantSettingAsync(UserChantSetting setting, CancellationToken cancellationToken = default)
        => await Context.Set<UserChantSetting>().AddAsync(setting, cancellationToken);

    public void UpdateStreak(SadhanaStreak streak)
        => Context.Set<SadhanaStreak>().Update(streak);
}
