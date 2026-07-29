using Microsoft.EntityFrameworkCore;
using Sanathana.Companion.Domain.Entities;
using Sanathana.Companion.Domain.Interfaces;

namespace Sanathana.Companion.Infrastructure.Persistence.Repositories;

public class UserFavoriteRepository : BaseRepository<UserFavorite>, IUserFavoriteRepository
{
    public UserFavoriteRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IReadOnlyList<UserFavorite>> GetByUserAsync(Guid userId, CancellationToken cancellationToken = default)
        => await Set.AsNoTracking()
                    .Where(f => f.UserId == userId)
                    .OrderByDescending(f => f.CreatedDate)
                    .ToListAsync(cancellationToken);

    public async Task<UserFavorite?> GetAsync(Guid userId, string favoriteType, Guid itemId, CancellationToken cancellationToken = default)
        => await Set.FirstOrDefaultAsync(
            f => f.UserId == userId && f.FavoriteType == favoriteType && f.ItemId == itemId,
            cancellationToken);
}
