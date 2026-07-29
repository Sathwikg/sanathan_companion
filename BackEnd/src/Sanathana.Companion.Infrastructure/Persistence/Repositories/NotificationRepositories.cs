using Microsoft.EntityFrameworkCore;
using Sanathana.Companion.Domain.Entities;
using Sanathana.Companion.Domain.Interfaces;

namespace Sanathana.Companion.Infrastructure.Persistence.Repositories;

public class NotificationConfigRepository : BaseRepository<NotificationConfig>, INotificationConfigRepository
{
    public NotificationConfigRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IReadOnlyList<NotificationConfig>> GetAllWithModuleAsync(CancellationToken cancellationToken = default)
        => await Set.AsNoTracking()
                    .Include(c => c.MenuModule)
                    .OrderBy(c => c.DisplayOrder)
                    .ToListAsync(cancellationToken);

    public async Task<NotificationConfig?> GetByModuleAsync(Guid menuModuleId, CancellationToken cancellationToken = default)
        => await Set.FirstOrDefaultAsync(c => c.MenuModuleId == menuModuleId, cancellationToken);
}

public class UserNotificationRepository : BaseRepository<UserNotificationPreference>, IUserNotificationRepository
{
    public UserNotificationRepository(ApplicationDbContext context) : base(context) { }

    public async Task<UserNotificationSetting?> GetSettingAsync(Guid userId, CancellationToken cancellationToken = default)
        => await Context.Set<UserNotificationSetting>().FirstOrDefaultAsync(s => s.UserId == userId, cancellationToken);

    public async Task AddSettingAsync(UserNotificationSetting setting, CancellationToken cancellationToken = default)
        => await Context.Set<UserNotificationSetting>().AddAsync(setting, cancellationToken);

    public async Task<IReadOnlyList<UserNotificationPreference>> GetPreferencesAsync(Guid userId, CancellationToken cancellationToken = default)
        => await Set.Where(p => p.UserId == userId).ToListAsync(cancellationToken);
}
