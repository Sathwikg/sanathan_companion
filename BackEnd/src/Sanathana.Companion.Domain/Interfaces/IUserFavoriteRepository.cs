using Sanathana.Companion.Domain.Entities;

namespace Sanathana.Companion.Domain.Interfaces;

public interface IUserFavoriteRepository : IRepository<UserFavorite>
{
    /// <summary>All of a user's favorites.</summary>
    Task<IReadOnlyList<UserFavorite>> GetByUserAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>The favorite row for one item, if it exists.</summary>
    Task<UserFavorite?> GetAsync(Guid userId, string favoriteType, Guid itemId, CancellationToken cancellationToken = default);
}
