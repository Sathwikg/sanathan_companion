using Sanathana.Companion.Application.Common;
using Sanathana.Companion.Application.DTOs.Favorites;
using Sanathana.Companion.Application.Interfaces;
using Sanathana.Companion.Domain.Entities;
using Sanathana.Companion.Domain.Exceptions;
using Sanathana.Companion.Domain.Interfaces;

namespace Sanathana.Companion.Application.Services;

public class FavoritesService : IFavoritesService
{
    private readonly IUnitOfWork _uow;

    public FavoritesService(IUnitOfWork uow) => _uow = uow;

    public async Task<bool> ToggleAsync(Guid userId, string type, Guid itemId, CancellationToken cancellationToken = default)
    {
        if (!FavoriteTypes.IsValid(type))
            throw new BadRequestException("Unknown favorite type.");

        var favType = FavoriteTypes.Normalize(type);

        var exists = favType == FavoriteTypes.Chant
            ? await _uow.ChantConfigs.AnyAsync(c => c.Id == itemId, cancellationToken)
            : await _uow.Deities.AnyAsync(d => d.Id == itemId, cancellationToken);
        if (!exists)
            throw new NotFoundException("The item you tried to favorite was not found.");

        var existing = await _uow.Favorites.GetAsync(userId, favType, itemId, cancellationToken);
        if (existing is not null)
        {
            _uow.Favorites.Remove(existing);
            await _uow.SaveChangesAsync(cancellationToken);
            return false;
        }

        await _uow.Favorites.AddAsync(new UserFavorite
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            FavoriteType = favType,
            ItemId = itemId
        }, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<FavoriteIdsDto> GetIdsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var favs = await _uow.Favorites.GetByUserAsync(userId, cancellationToken);
        return new FavoriteIdsDto
        {
            ChantIds = favs.Where(f => f.FavoriteType == FavoriteTypes.Chant).Select(f => f.ItemId).ToList(),
            DeityIds = favs.Where(f => f.FavoriteType == FavoriteTypes.Deity).Select(f => f.ItemId).ToList()
        };
    }

    public async Task<FavoritesDto> GetFavoritesAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var favs = await _uow.Favorites.GetByUserAsync(userId, cancellationToken);
        var chantIds = favs.Where(f => f.FavoriteType == FavoriteTypes.Chant).Select(f => f.ItemId).ToHashSet();
        var deityIds = favs.Where(f => f.FavoriteType == FavoriteTypes.Deity).Select(f => f.ItemId).ToHashSet();

        var result = new FavoritesDto();
        if (chantIds.Count == 0 && deityIds.Count == 0) return result;

        var deities = await _uow.Deities.ListWithoutImageAsync(cancellationToken);
        var deityNames = deities.ToDictionary(d => d.Id, d => d.Name);

        if (chantIds.Count > 0)
        {
            var configs = (await _uow.ChantConfigs.GetFilteredAsync(null, null, null, cancellationToken))
                .Where(c => c.IsActive && chantIds.Contains(c.Id)).ToList();
            var categoryNames = (await _uow.Chants.GetAllOrderedAsync(cancellationToken))
                .ToDictionary(c => c.Id, c => c.Name);

            result.Chants = configs
                .OrderBy(c => c.Name)
                .Select(c => new FavoriteChantDto
                {
                    ChantConfigId = c.Id,
                    Name = c.Name,
                    CategoryName = c.Chant?.Name ?? categoryNames.GetValueOrDefault(c.ChantId, string.Empty),
                    DeityNames = CsvIds.Split(c.DeityIds).Where(deityNames.ContainsKey).Select(id => deityNames[id]).ToList(),
                    HasAudio = c.AudioContentType != null
                }).ToList();
        }

        if (deityIds.Count > 0)
        {
            result.Gods = deities
                .Where(d => d.IsActive && deityIds.Contains(d.Id))
                .OrderBy(d => d.Name)
                .Select(d => new FavoriteDeityDto
                {
                    Id = d.Id,
                    Name = d.Name,
                    DeityType = d.DeityType,
                    Description = d.Description,
                    HasImage = d.ImageContentType != null
                }).ToList();
        }

        return result;
    }
}
