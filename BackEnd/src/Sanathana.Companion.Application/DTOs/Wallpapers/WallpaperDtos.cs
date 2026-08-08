using Sanathana.Companion.Application.Common.Translation;

namespace Sanathana.Companion.Application.DTOs.Wallpapers;

/// <summary>One wallpaper's metadata. The image itself comes from the image endpoint.</summary>
public class WallpaperDto
{
    public Guid Id { get; set; }
    public Guid DeityId { get; set; }

    /// <summary>Display only — the screens identify the deity by <see cref="DeityId"/>.</summary>
    [Translatable(Category = "deity")]
    public string DeityName { get; set; } = string.Empty;

    /// <summary>Free text typed by an admin, so it is never dictionary-translated.</summary>
    [NoTranslate]
    public string? Title { get; set; }

    public string ContentType { get; set; } = string.Empty;
    public int FileSize { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; }
}

/// <summary>A deity in the wallpaper picker, with how many wallpapers it has.</summary>
public class WallpaperDeityDto
{
    public Guid DeityId { get; set; }

    [Translatable("Deity", nameof(DeityId))]
    public string Name { get; set; } = string.Empty;

    [Translatable(Category = "deity")]
    public string DeityType { get; set; } = string.Empty;

    public int WallpaperCount { get; set; }
    public bool HasProfileImage { get; set; }
}

/// <summary>One image in an upload batch.</summary>
public class WallpaperUploadItemDto
{
    public string? Title { get; set; }

    /// <summary>Image as a data URI ("data:image/webp;base64,…").</summary>
    public string ImageBase64 { get; set; } = string.Empty;
}

/// <summary>Upload several wallpapers against one deity in a single call.</summary>
public class CreateWallpapersDto
{
    public Guid DeityId { get; set; }
    public List<WallpaperUploadItemDto> Items { get; set; } = new();
}

public class UpdateWallpaperDto
{
    public string? Title { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>What an upload batch actually stored, so the UI can report partial success honestly.</summary>
public class WallpaperUploadResultDto
{
    public int Added { get; set; }
    public List<string> Rejected { get; set; } = new();
}
