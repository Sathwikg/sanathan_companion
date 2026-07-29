using Sanathana.Companion.Domain.Common;

namespace Sanathana.Companion.Domain.Entities;

/// <summary>
/// A configured chant entry that belongs to a chant category (<see cref="Entities.Chant"/>),
/// e.g. several "Ashtakam" entries under the Ashtakam category.
/// </summary>
public class ChantConfig : BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The chant category (Ashtakam, Stotra, Chalisa…).</summary>
    public Guid ChantId { get; set; }
    public Chant? Chant { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>Comma-separated deity ids (FK values into Deities), e.g. "guid1,guid2".</summary>
    public string? DeityIds { get; set; }

    /// <summary>The chant body as sanitized HTML produced by the rich-text editor.</summary>
    public string ChantText { get; set; } = string.Empty;

    // ---- Audio metadata. The bytes live in ChantConfigAudio so they are never
    // loaded by list/edit queries. ----
    public string? AudioFileName { get; set; }
    public string? AudioContentType { get; set; }
    public long? AudioSizeBytes { get; set; }

    // ---- Optional time configuration ----
    public TimeOnly? FromTime { get; set; }
    public TimeOnly? ToTime { get; set; }
    public string? TimeDescription { get; set; }

    public bool IsActive { get; set; } = true;

    public ChantConfigAudio? Audio { get; set; }
}
