using Sanathana.Companion.Domain.Common;

namespace Sanathana.Companion.Domain.Entities;

/// <summary>Festival master record — a named festival on a specific date within a year.</summary>
public class Festival : BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Year { get; set; }
    public DateOnly Date { get; set; }

    /// <summary>Comma-separated region FK ids (Region.Id GUIDs).</summary>
    public string? Regions { get; set; }

    public bool IsActive { get; set; } = true;
}
