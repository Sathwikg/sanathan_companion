using Sanathana.Companion.Domain.Common;

namespace Sanathana.Companion.Domain.Entities;

/// <summary>Puja master record — a ritual, mapped to the festival it belongs to.</summary>
/// <remarks>
/// The link is to a specific <see cref="Festival"/> row, and a festival row is year-scoped
/// ("Diwali 2026" and "Diwali 2027" are separate rows). So a puja belongs to one year's
/// observance; the picker shows the year alongside the name to keep that unambiguous.
/// </remarks>
public class Puja : BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    /// <summary>Short description of what the puja involves.</summary>
    public string? Description { get; set; }

    public Guid FestivalId { get; set; }
    public Festival? Festival { get; set; }

    public bool IsActive { get; set; } = true;
}
