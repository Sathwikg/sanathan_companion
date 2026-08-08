using Sanathana.Companion.Domain.Common;

namespace Sanathana.Companion.Domain.Entities;

/// <summary>Puja master record — a ritual, optionally mapped to a festival and/or a deity.</summary>
/// <remarks>
/// Both mappings are optional: plenty of pujas are tied to neither a particular festival nor a
/// single deity. When a festival IS set, note that festival rows are year-scoped ("Diwali 2026"
/// and "Diwali 2027" are separate rows), so the puja belongs to that year's observance.
/// </remarks>
public class Puja : BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    /// <summary>Short description of what the puja involves.</summary>
    public string? Description { get; set; }

    /// <summary>Optional — the festival this puja belongs to.</summary>
    public Guid? FestivalId { get; set; }
    public Festival? Festival { get; set; }

    /// <summary>Optional — the deity this puja is offered to.</summary>
    public Guid? DeityId { get; set; }
    public Deity? Deity { get; set; }

    public bool IsActive { get; set; } = true;
}
