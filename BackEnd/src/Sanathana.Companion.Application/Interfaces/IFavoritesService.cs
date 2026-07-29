using Sanathana.Companion.Application.DTOs.Favorites;

namespace Sanathana.Companion.Application.Interfaces;

public interface IFavoritesService
{
    /// <summary>Adds the favorite if absent, removes it if present. Returns the new state.</summary>
    Task<bool> ToggleAsync(Guid userId, string type, Guid itemId, CancellationToken cancellationToken = default);

    /// <summary>The ids the user has favorited, grouped by type.</summary>
    Task<FavoriteIdsDto> GetIdsAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>The user's favorites resolved to display data (skips items that no longer exist / are inactive).</summary>
    Task<FavoritesDto> GetFavoritesAsync(Guid userId, CancellationToken cancellationToken = default);
}
