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
    /// <summary>The stored bytes, or nulls when the row is missing or not published.</summary>
    /// <param name="includeInactive">
    /// Serves a deactivated row's bytes. Only an administrative caller should ask; there is no way
    /// to tell from the request itself, because these bytes are fetched by an &lt;img&gt; or
    /// &lt;audio&gt; element that carries no bearer token.
    /// </param>
    Task<(byte[]? Data, string? ContentType, string? Title)> GetImageAsync(Guid id, bool includeInactive = false, CancellationToken cancellationToken = default);

    /// <summary>How many wallpapers each deity has, so the picker can show counts without N queries.</summary>
    Task<IReadOnlyDictionary<Guid, int>> GetCountsByDeityAsync(bool activeOnly, CancellationToken cancellationToken = default);

    /// <summary>Highest DisplayOrder currently used for a deity, so new uploads append.</summary>
    Task<int> GetMaxDisplayOrderAsync(Guid deityId, CancellationToken cancellationToken = default);
}
