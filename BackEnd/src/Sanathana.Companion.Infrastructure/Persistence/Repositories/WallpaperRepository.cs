using Microsoft.EntityFrameworkCore;
using Sanathana.Companion.Domain.Entities;
using Sanathana.Companion.Domain.Interfaces;

namespace Sanathana.Companion.Infrastructure.Persistence.Repositories;

public class WallpaperRepository : BaseRepository<Wallpaper>, IWallpaperRepository
{
    public WallpaperRepository(ApplicationDbContext context) : base(context) { }

    /// <summary>
    /// Every read here projects away <see cref="Wallpaper.ImageData"/>. Selecting the entity would
    /// drag every blob across the wire just to render a list of thumbnails; the images themselves
    /// are fetched one at a time by the image endpoint, which the browser then caches.
    /// </summary>
    private static Wallpaper WithoutBlob(Wallpaper w) => new()
    {
        Id = w.Id,
        DeityId = w.DeityId,
        Title = w.Title,
        ImageContentType = w.ImageContentType,
        FileSize = w.FileSize,
        DisplayOrder = w.DisplayOrder,
        IsActive = w.IsActive,
        CreatedBy = w.CreatedBy,
        CreatedDate = w.CreatedDate
    };

    public async Task<IReadOnlyList<Wallpaper>> GetByDeityAsync(
        Guid deityId, bool activeOnly, CancellationToken cancellationToken = default)
        => await Set.AsNoTracking()
            .Where(w => w.DeityId == deityId && (!activeOnly || w.IsActive))
            .OrderBy(w => w.DisplayOrder).ThenBy(w => w.CreatedDate)
            .Select(w => WithoutBlob(w))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Wallpaper>> GetAllMetadataAsync(CancellationToken cancellationToken = default)
        => await Set.AsNoTracking()
            .OrderBy(w => w.DeityId).ThenBy(w => w.DisplayOrder)
            .Select(w => WithoutBlob(w))
            .ToListAsync(cancellationToken);

    public async Task<(byte[]? Data, string? ContentType, string? Title)> GetImageAsync(
        Guid id, bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        var row = await Set.AsNoTracking()
            .Where(w => w.Id == id && (includeInactive || w.IsActive))
            .Select(w => new { w.ImageData, w.ImageContentType, w.Title })
            .FirstOrDefaultAsync(cancellationToken);

        return row is null ? (null, null, null) : (row.ImageData, row.ImageContentType, row.Title);
    }

    public async Task<IReadOnlyDictionary<Guid, int>> GetCountsByDeityAsync(
        bool activeOnly, CancellationToken cancellationToken = default)
        => await Set.AsNoTracking()
            .Where(w => !activeOnly || w.IsActive)
            .GroupBy(w => w.DeityId)
            .Select(g => new { DeityId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.DeityId, x => x.Count, cancellationToken);

    public async Task<int> GetMaxDisplayOrderAsync(Guid deityId, CancellationToken cancellationToken = default)
        => await Set.AsNoTracking()
            .Where(w => w.DeityId == deityId)
            .Select(w => (int?)w.DisplayOrder)
            .MaxAsync(cancellationToken) ?? 0;
}
