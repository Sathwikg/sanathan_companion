using Microsoft.EntityFrameworkCore;
using Sanathana.Companion.Domain.Entities;
using Sanathana.Companion.Domain.Interfaces;
using Sanathana.Companion.Infrastructure.Persistence;

namespace Sanathana.Companion.Infrastructure.Persistence.Repositories;

public class DeviceTokenRepository : BaseRepository<DeviceToken>, IDeviceTokenRepository
{
    public DeviceTokenRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IReadOnlyList<DeviceToken>> GetActiveByUserAsync(Guid userId, CancellationToken cancellationToken = default)
        => await Set.AsNoTracking()
            .Where(t => t.UserId == userId && t.IsActive)
            .OrderByDescending(t => t.LastUsedAt ?? t.CreatedDate)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<DeviceToken>> GetActiveByPlatformAsync(int platform, CancellationToken cancellationToken = default)
        => await Set.AsNoTracking()
            .Where(t => t.Platform == platform && t.IsActive)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<DeviceToken>> GetActiveByModuleAsync(Guid menuModuleId, int? platform = null, CancellationToken cancellationToken = default)
    {
        // Get user IDs who have this module's notifications enabled
        var userIds = await Context.UserNotificationPreferences
            .Where(p => p.NotificationConfig != null 
                && p.NotificationConfig.MenuModuleId == menuModuleId 
                && p.IsEnabled)
            .Join(
                Context.Users.Where(u => u.IsActive),
                p => p.UserId,
                u => u.UserId,
                (p, u) => u.UserId
            )
            .Distinct()
            .ToListAsync(cancellationToken);

        if (!userIds.Any()) return new List<DeviceToken>();

        var query = Set.AsNoTracking()
            .Where(t => t.IsActive && userIds.Contains(t.UserId));

        if (platform.HasValue)
            query = query.Where(t => t.Platform == platform.Value);

        return await query.ToListAsync(cancellationToken);
    }

    public async Task<DeviceToken?> GetByTokenAsync(string token, CancellationToken cancellationToken = default)
        => await Set.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Token == token, cancellationToken);

    public async Task<int> MarkInactiveAsync(string token, string reason, CancellationToken cancellationToken = default)
        => await Set.Where(t => t.Token == token)
            .ExecuteUpdateAsync(s => s
                .SetProperty(t => t.IsActive, false)
                .SetProperty(t => t.LastFailureReason, reason), cancellationToken);

    public async Task<int> IncrementFailureCountAsync(string token, string reason, CancellationToken cancellationToken = default)
        => await Set.Where(t => t.Token == token)
            .ExecuteUpdateAsync(s => s
                .SetProperty(t => t.FailureCount, t => t.FailureCount + 1)
                .SetProperty(t => t.LastFailureReason, reason), cancellationToken);

    public async Task<int> ResetFailureCountAsync(string token, CancellationToken cancellationToken = default)
        => await Set.Where(t => t.Token == token)
            .ExecuteUpdateAsync(s => s
                .SetProperty(t => t.FailureCount, 0)
                .SetProperty(t => t.LastFailureReason, (string?)null), cancellationToken);

    public async Task<int> UpdateLastUsedAsync(string token, DateTime usedAt, CancellationToken cancellationToken = default)
        => await Set.Where(t => t.Token == token)
            .ExecuteUpdateAsync(s => s
                .SetProperty(t => t.LastUsedAt, usedAt), cancellationToken);

    public async Task<int> DeleteInactiveOlderThanAsync(DateTime cutoff, CancellationToken cancellationToken = default)
        => await Set.Where(t => !t.IsActive && t.CreatedDate < cutoff)
            .ExecuteDeleteAsync(cancellationToken);

    public async Task<IReadOnlyList<DeviceToken>> GetAllAsync(CancellationToken cancellationToken = default)
        => await Set.AsNoTracking().ToListAsync(cancellationToken);
}