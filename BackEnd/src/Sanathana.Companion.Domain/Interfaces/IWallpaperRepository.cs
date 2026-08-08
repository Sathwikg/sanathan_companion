using Sanathana.Companion.Domain.Entities;

namespace Sanathana.Companion.Domain.Interfaces;

public interface IWallpaperRepository : IRepository<Wallpaper>
{
    /// <summary>
    /// Wallpapers for one deity, newest ordering first by DisplayOrder. Projected without the blob —
    /// a gallery of twenty wallpapers must not pull twenty images into memory to render thumbnails.
    /// </summary>
    Task<IReadOnlyList<Wallpaper>> GetByDeityAsync(Guid deityId, bool activeOnly, CancellationToken cancellationToken = default);

    /// <summary>Every wallpaper, metadata only, for the admin list.</summary>
    Task<IReadOnlyList<Wallpaper>> GetAllMetadataAsync(CancellationToken cancellationToken = default);

    /// <summary>Just the blob and its content type, for the image and download endpoints.</summary>
    Task<(byte[]? Data, string? ContentType, string? Title)> GetImageAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>How many wallpapers each deity has, so the picker can show counts without N queries.</summary>
    Task<IReadOnlyDictionary<Guid, int>> GetCountsByDeityAsync(bool activeOnly, CancellationToken cancellationToken = default);

    /// <summary>Highest DisplayOrder currently used for a deity, so new uploads append.</summary>
    Task<int> GetMaxDisplayOrderAsync(Guid deityId, CancellationToken cancellationToken = default);
}
