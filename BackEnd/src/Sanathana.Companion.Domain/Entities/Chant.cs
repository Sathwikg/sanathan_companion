using Sanathana.Companion.Domain.Common;

namespace Sanathana.Companion.Domain.Entities;

/// <summary>Chant master record.</summary>
public class Chant : BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>Whether this chant has a repetition count.</summary>
    public bool HasCount { get; set; }

    /// <summary>The count (only meaningful when <see cref="HasCount"/> is true).</summary>
    public int? Count { get; set; }

    public bool IsActive { get; set; } = true;
}
