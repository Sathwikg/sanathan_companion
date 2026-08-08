using FluentValidation;
using Sanathana.Companion.Application.DTOs.Wallpapers;
using Sanathana.Companion.Application.Interfaces;
using Sanathana.Companion.Domain.Entities;
using Sanathana.Companion.Domain.Exceptions;
using Sanathana.Companion.Domain.Interfaces;

namespace Sanathana.Companion.Application.Services;

public class WallpaperService : IWallpaperService
{
    /// <summary>
    /// Only formats a browser will render inline. The content type is echoed back on the image
    /// response, so accepting whatever the client claims would let someone store, say,
    /// "text/html" against an image body and have it served from our own origin.
    /// </summary>
    private static readonly HashSet<string> AllowedContentTypes =
        new(StringComparer.OrdinalIgnoreCase) { "image/webp", "image/jpeg", "image/png" };

    /// <summary>Wallpapers are phone-screen sized; anything larger is a mistake or an attack.</summary>
    private const int MaxBytes = 8 * 1024 * 1024;

    /// <summary>Caps one request, so a batch upload cannot be used to exhaust the database.</summary>
    private const int MaxItemsPerBatch = 25;

    private const int MaxTitleLength = 150;

    private readonly IUnitOfWork _uow;

    public WallpaperService(IUnitOfWork uow) => _uow = uow;

    public async Task<IReadOnlyList<WallpaperDeityDto>> GetDeitiesAsync(
        bool onlyWithWallpapers, CancellationToken cancellationToken = default)
    {
        // ListWithoutImageAsync deliberately leaves the blob behind, so HasProfileImage is derived
        // from the content type — which that projection does carry, and which is only set when an
        // image exists.
        var deities = await _uow.Deities.ListWithoutImageAsync(cancellationToken);
        var counts = await _uow.Wallpapers.GetCountsByDeityAsync(onlyWithWallpapers, cancellationToken);

        return deities
            .Where(d => d.IsActive)
            .Select(d => new WallpaperDeityDto
            {
                DeityId = d.Id,
                Name = d.Name,
                DeityType = d.DeityType,
                WallpaperCount = counts.TryGetValue(d.Id, out var c) ? c : 0,
                HasProfileImage = !string.IsNullOrEmpty(d.ImageContentType)
            })
            .Where(d => !onlyWithWallpapers || d.WallpaperCount > 0)
            .ToList();
    }

    public async Task<IReadOnlyList<WallpaperDto>> GetByDeityAsync(
        Guid deityId, bool activeOnly, CancellationToken cancellationToken = default)
    {
        var deity = await _uow.Deities.GetByIdAsync(deityId, cancellationToken)
            ?? throw new NotFoundException($"Deity '{deityId}' was not found.");

        var rows = await _uow.Wallpapers.GetByDeityAsync(deityId, activeOnly, cancellationToken);
        return rows.Select(w => Map(w, deity.Name)).ToList();
    }

    public async Task<WallpaperDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _uow.Wallpapers.GetByIdAsync(id, cancellationToken);
        if (entity is null) return null;

        var deity = await _uow.Deities.GetByIdAsync(entity.DeityId, cancellationToken);
        return Map(entity, deity?.Name ?? string.Empty);
    }

    public Task<(byte[]? Data, string? ContentType, string? Title)> GetImageAsync(
        Guid id, CancellationToken cancellationToken = default)
        => _uow.Wallpapers.GetImageAsync(id, cancellationToken);

    public async Task<WallpaperUploadResultDto> CreateAsync(
        CreateWallpapersDto dto, CancellationToken cancellationToken = default)
    {
        _ = await _uow.Deities.GetByIdAsync(dto.DeityId, cancellationToken)
            ?? throw new NotFoundException($"Deity '{dto.DeityId}' was not found.");

        if (dto.Items.Count == 0)
            throw new ValidationException("Select at least one image to upload.");

        if (dto.Items.Count > MaxItemsPerBatch)
            throw new ValidationException($"Upload at most {MaxItemsPerBatch} wallpapers at a time.");

        var result = new WallpaperUploadResultDto();
        var order = await _uow.Wallpapers.GetMaxDisplayOrderAsync(dto.DeityId, cancellationToken);

        for (var i = 0; i < dto.Items.Count; i++)
        {
            var item = dto.Items[i];
            var label = string.IsNullOrWhiteSpace(item.Title) ? $"Image {i + 1}" : item.Title!.Trim();

            var parsed = ParseImageDataUri(item.ImageBase64, out var reason);
            if (parsed is null)
            {
                // One bad file must not discard the rest of the batch — report it instead.
                result.Rejected.Add($"{label}: {reason}");
                continue;
            }

            var (bytes, contentType) = parsed.Value;

            await _uow.Wallpapers.AddAsync(new Wallpaper
            {
                DeityId = dto.DeityId,
                Title = Truncate(item.Title),
                ImageData = bytes,
                ImageContentType = contentType,
                FileSize = bytes.Length,
                DisplayOrder = ++order,
                IsActive = true
            }, cancellationToken);

            result.Added++;
        }

        if (result.Added > 0) await _uow.SaveChangesAsync(cancellationToken);
        return result;
    }

    public async Task UpdateAsync(Guid id, UpdateWallpaperDto dto, CancellationToken cancellationToken = default)
    {
        var entity = await _uow.Wallpapers.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Wallpaper '{id}' was not found.");

        entity.Title = Truncate(dto.Title);
        entity.DisplayOrder = dto.DisplayOrder;
        entity.IsActive = dto.IsActive;

        _uow.Wallpapers.Update(entity);
        await _uow.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _uow.Wallpapers.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Wallpaper '{id}' was not found.");

        _uow.Wallpapers.Remove(entity);
        await _uow.SaveChangesAsync(cancellationToken);
    }

    private static WallpaperDto Map(Wallpaper w, string deityName) => new()
    {
        Id = w.Id,
        DeityId = w.DeityId,
        DeityName = deityName,
        Title = w.Title,
        ContentType = w.ImageContentType,
        FileSize = w.FileSize,
        DisplayOrder = w.DisplayOrder,
        IsActive = w.IsActive
    };

    private static string? Truncate(string? title)
    {
        var t = title?.Trim();
        if (string.IsNullOrEmpty(t)) return null;
        return t.Length <= MaxTitleLength ? t : t[..MaxTitleLength];
    }

    /// <summary>
    /// Decodes "data:image/webp;base64,…" and refuses anything that is not an allowed image within
    /// the size cap. Validating the declared type is not enough on its own, so the magic bytes are
    /// checked too — the two must agree before the blob is stored.
    /// </summary>
    private static (byte[] Bytes, string ContentType)? ParseImageDataUri(string? dataUri, out string reason)
    {
        reason = string.Empty;

        if (string.IsNullOrWhiteSpace(dataUri)) { reason = "no image data."; return null; }

        var comma = dataUri.IndexOf(',');
        if (comma < 0 || !dataUri.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            reason = "not a data URI.";
            return null;
        }

        var meta = dataUri[..comma];
        var colon = meta.IndexOf(':');
        var semi = meta.IndexOf(';');
        if (colon < 0 || semi <= colon) { reason = "malformed data URI header."; return null; }

        var contentType = meta[(colon + 1)..semi].Trim();
        if (!AllowedContentTypes.Contains(contentType))
        {
            reason = $"'{contentType}' is not an accepted image type.";
            return null;
        }

        byte[] bytes;
        try { bytes = Convert.FromBase64String(dataUri[(comma + 1)..]); }
        catch { reason = "image data was not valid base64."; return null; }

        if (bytes.Length == 0) { reason = "image was empty."; return null; }
        if (bytes.Length > MaxBytes)
        {
            reason = $"image is {bytes.Length / (1024 * 1024)}MB; the limit is {MaxBytes / (1024 * 1024)}MB.";
            return null;
        }

        if (!MatchesMagicBytes(bytes, contentType))
        {
            reason = "file contents do not match the declared image type.";
            return null;
        }

        return (bytes, contentType.ToLowerInvariant());
    }

    /// <summary>Confirms the bytes really are the image format the caller claimed.</summary>
    private static bool MatchesMagicBytes(byte[] b, string contentType) => contentType.ToLowerInvariant() switch
    {
        // RIFF....WEBP
        "image/webp" => b.Length > 12
            && b[0] == 0x52 && b[1] == 0x49 && b[2] == 0x46 && b[3] == 0x46
            && b[8] == 0x57 && b[9] == 0x45 && b[10] == 0x42 && b[11] == 0x50,

        "image/jpeg" => b.Length > 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF,

        "image/png" => b.Length > 8
            && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47
            && b[4] == 0x0D && b[5] == 0x0A && b[6] == 0x1A && b[7] == 0x0A,

        _ => false
    };
}
