using Microsoft.EntityFrameworkCore;
using Sanathana.Companion.Domain.Common;
using Sanathana.Companion.Domain.Entities;
using Sanathana.Companion.Domain.Interfaces;

namespace Sanathana.Companion.Infrastructure.Persistence.Repositories;

public class UserRepository : BaseRepository<User>, IUserRepository
{
    public UserRepository(ApplicationDbContext context) : base(context) { }

    public async Task<User?> GetByEmailOrMobileAsync(string credential, CancellationToken cancellationToken = default)
    {
        var trimmed = (credential ?? string.Empty).Trim();
        var asEmail = CredentialNormalizer.Email(trimmed);

        // Anything with an @ is an address and nothing else; the rest is worth trying as a number
        // too. The email branch stays unconditional because the seeded administrator signs in as
        // the literal "admin", which is neither.
        var asMobile = trimmed.Contains('@') ? null : CredentialNormalizer.Mobile(trimmed);

        return asMobile is null
            ? await Set.Include(u => u.Role).FirstOrDefaultAsync(u => u.Email == asEmail, cancellationToken)
            : await Set.Include(u => u.Role)
                       .FirstOrDefaultAsync(u => u.Email == asEmail || u.MobileNumber == asMobile, cancellationToken);
    }

    public async Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default)
    {
        var normalised = CredentialNormalizer.Email(email);
        return await Set.AnyAsync(u => u.Email == normalised, cancellationToken);
    }

    public async Task<bool> MobileExistsAsync(string mobile, CancellationToken cancellationToken = default)
    {
        // Normalised into a local first, so EF translates a constant rather than trying to
        // translate the helper itself.
        var normalised = CredentialNormalizer.Mobile(mobile);
        return normalised is not null && await Set.AnyAsync(u => u.MobileNumber == normalised, cancellationToken);
    }

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
