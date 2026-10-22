using Sanathana.Companion.Domain.Entities;

namespace Sanathana.Companion.Domain.Interfaces;

/// <summary>Aggregate sadhana figures across all seekers, for the admin dashboard.</summary>
public sealed record SadhanaTotals(
    int TotalMalas,
    int TotalDaysPracticed,
    int LongestStreak,
    int ActiveToday,
    long TotalJapa,
    int TotalSessions);

public interface ISadhanaRepository : IRepository<SadhanaLog>
{
    /// <summary>Community-wide sadhana totals. <paramref name="today"/> should be the local (IST) date.</summary>
    Task<SadhanaTotals> GetTotalsAsync(DateOnly today, CancellationToken cancellationToken = default);

    Task<SadhanaLog?> GetLogAsync(Guid userId, DateOnly date, Guid chantConfigId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SadhanaLog>> GetLogsForDateAsync(Guid userId, DateOnly date, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SadhanaLog>> GetHistoryAsync(Guid userId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default);

    Task<SadhanaStreak?> GetStreakAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Streaks for many users in one round trip (avoids N+1 on the user list).</summary>
    Task<IReadOnlyList<SadhanaStreak>> GetStreaksAsync(IEnumerable<Guid> userIds, CancellationToken cancellationToken = default);

    Task AddStreakAsync(SadhanaStreak streak, CancellationToken cancellationToken = default);

    /// <summary>The seeker's own settings for a chant, tracked for update; null means the defaults.</summary>
    Task<UserChantSetting?> GetChantSettingAsync(Guid userId, Guid chantConfigId, CancellationToken cancellationToken = default);

    /// <summary>Every chant the seeker has customised, for the lists' "1 mala = N".</summary>
    Task<IReadOnlyList<UserChantSetting>> GetChantSettingsAsync(Guid userId, CancellationToken cancellationToken = default);

    Task AddChantSettingAsync(UserChantSetting setting, CancellationToken cancellationToken = default);

    void UpdateStreak(SadhanaStreak streak);
}
