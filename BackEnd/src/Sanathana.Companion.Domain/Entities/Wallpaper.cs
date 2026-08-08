using Sanathana.Companion.Domain.Common;

namespace Sanathana.Companion.Domain.Entities;

/// <summary>
/// A downloadable wallpaper belonging to one deity. A deity can have many.
/// </summary>
/// <remarks>
/// The image lives in the row as a blob, the same way <see cref="Deity.ImageData"/> does, so a
/// deployment needs no writable file storage or CDN. Wallpapers are larger than profile pictures,
/// so the service caps the accepted size rather than trusting the client to have compressed well.
/// </remarks>
public class Wallpaper : BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid DeityId { get; set; }
    public Deity? Deity { get; set; }

    /// <summary>Shown under the thumbnail and used to name the downloaded file.</summary>
    public string? Title { get; set; }

    public byte[] ImageData { get; set; } = Array.Empty<byte>();
    public string ImageContentType { get; set; } = "image/webp";

    /// <summary>Byte length of <see cref="ImageData"/>, so listings need not read the blob.</summary>
    public int FileSize { get; set; }

    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
}
