using Sanathana.Companion.Application.DTOs.Wallpapers;

namespace Sanathana.Companion.Application.Interfaces;

public interface IWallpaperService
{
    /// <summary>Deities that have at least one wallpaper (or all of them, for the admin picker).</summary>
    Task<IReadOnlyList<WallpaperDeityDto>> GetDeitiesAsync(bool onlyWithWallpapers, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WallpaperDto>> GetByDeityAsync(Guid deityId, bool activeOnly, CancellationToken cancellationToken = default);

    Task<WallpaperDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>The stored bytes, or nulls when the row is missing or not published.</summary>
    Task<(byte[]? Data, string? ContentType, string? Title)> GetImageAsync(Guid id, bool includeInactive = false, CancellationToken cancellationToken = default);

    /// <summary>Stores a batch, skipping any item that fails validation rather than failing the lot.</summary>
    Task<WallpaperUploadResultDto> CreateAsync(CreateWallpapersDto dto, CancellationToken cancellationToken = default);

    Task UpdateAsync(Guid id, UpdateWallpaperDto dto, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
