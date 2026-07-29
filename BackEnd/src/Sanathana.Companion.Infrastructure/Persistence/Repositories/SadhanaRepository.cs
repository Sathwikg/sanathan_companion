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

    public void UpdateStreak(SadhanaStreak streak)
        => Context.Set<SadhanaStreak>().Update(streak);
}
