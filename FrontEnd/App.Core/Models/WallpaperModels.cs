namespace App.Core.Models;

/// <summary>A deity in the wallpaper picker, with how many wallpapers it has.</summary>
public class WallpaperDeityModel
{
    public Guid DeityId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string DeityType { get; set; } = string.Empty;
    public int WallpaperCount { get; set; }
    public bool HasProfileImage { get; set; }
}

/// <summary>One wallpaper's metadata; the image comes from the API's image endpoint.</summary>
public class WallpaperModel
{
    public Guid Id { get; set; }
    public Guid DeityId { get; set; }
    public string DeityName { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string ContentType { get; set; } = string.Empty;
    public int FileSize { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; }

    /// <summary>Human-readable size for the gallery caption.</summary>
    public string SizeLabel => FileSize >= 1024 * 1024
        ? $"{FileSize / (1024.0 * 1024.0):0.#} MB"
        : $"{Math.Max(1, FileSize / 1024)} KB";
}

public class WallpaperUploadItem
{
    public string? Title { get; set; }
    public string ImageBase64 { get; set; } = string.Empty;
}

public class CreateWallpapersRequest
{
    public Guid DeityId { get; set; }
    public List<WallpaperUploadItem> Items { get; set; } = new();
}

public class UpdateWallpaperRequest
{
    public string? Title { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>What the server actually stored — some items in a batch may have been rejected.</summary>
public class WallpaperUploadResult
{
    public int Added { get; set; }
    public List<string> Rejected { get; set; } = new();
}
