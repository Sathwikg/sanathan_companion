using Microsoft.EntityFrameworkCore;
using Sanathana.Companion.Domain.Entities;
using Sanathana.Companion.Domain.Interfaces;

namespace Sanathana.Companion.Infrastructure.Persistence.Repositories;

public class UserRepository : BaseRepository<User>, IUserRepository
{
    public UserRepository(ApplicationDbContext context) : base(context) { }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
        => await Set.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

    public async Task<User?> GetByEmailOrMobileAsync(string credential, CancellationToken cancellationToken = default)
        => await Set.Include(u => u.Role)
                    .FirstOrDefaultAsync(u => u.Email == credential || u.MobileNumber == credential, cancellationToken);

    public async Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default)
        => await Set.AnyAsync(u => u.Email == email, cancellationToken);

    public async Task<IReadOnlyList<User>> GetAllWithRolesAsync(CancellationToken cancellationToken = default)
        => await Set.AsNoTracking().Include(u => u.Role)
            .OrderByDescending(u => u.CreatedDate).ToListAsync(cancellationToken);

    public async Task<User?> GetWithRoleAsync(Guid userId, CancellationToken cancellationToken = default)
        => await Set.AsNoTracking().Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken);

    public async Task PurgeUserDataAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        // Order matters only for feedback, whose FK is Restrict; the rest would cascade, but doing
        // every one explicitly means the set of things erased is readable here rather than spread
        // across six entity configurations.
        Context.SadhanaLogs.RemoveRange(
            await Context.SadhanaLogs.Where(x => x.UserId == userId).ToListAsync(cancellationToken));

        Context.SadhanaStreaks.RemoveRange(
            await Context.SadhanaStreaks.Where(x => x.UserId == userId).ToListAsync(cancellationToken));

        Context.UserFavorites.RemoveRange(
            await Context.UserFavorites.Where(x => x.UserId == userId).ToListAsync(cancellationToken));

        Context.UserNotificationPreferences.RemoveRange(
            await Context.UserNotificationPreferences.Where(x => x.UserId == userId).ToListAsync(cancellationToken));

        Context.UserNotificationSettings.RemoveRange(
            await Context.UserNotificationSettings.Where(x => x.UserId == userId).ToListAsync(cancellationToken));

        Context.Feedbacks.RemoveRange(
            await Context.Feedbacks.Where(x => x.UserId == userId).ToListAsync(cancellationToken));

        var user = await Set.FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken);
        if (user is not null) Set.Remove(user);
    }
}
