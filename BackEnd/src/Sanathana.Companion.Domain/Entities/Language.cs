using Sanathana.Companion.Domain.Common;

namespace Sanathana.Companion.Domain.Entities;

/// <summary>
/// A language master record. A language may be spoken across several regions, and a region
/// may have several languages — the many-to-many link is held here as a comma-separated
/// list of region ids, matching the convention used by Festivals.
/// </summary>
public class Language : BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    /// <summary>The language's name in its own script, e.g. "తెలుగు".</summary>
    public string? NativeName { get; set; }

    /// <summary>ISO 639 code, e.g. "te".</summary>
    public string? Code { get; set; }

    public string? Description { get; set; }

    /// <summary>Comma-separated region ids (FK values into Regions).</summary>
    public string? Regions { get; set; }

    public bool IsActive { get; set; } = true;
}
