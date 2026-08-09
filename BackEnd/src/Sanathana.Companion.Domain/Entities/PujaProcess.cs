using Sanathana.Companion.Domain.Common;

namespace Sanathana.Companion.Domain.Entities;

/// <summary>One item a devotee needs to gather before performing a puja.</summary>
public class PujaMaterial : BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid PujaId { get; set; }
    public Puja? Puja { get; set; }

    public string ItemName { get; set; } = string.Empty;

    /// <summary>Free text on purpose — "2", "a handful", "1 litre" are all legitimate answers.</summary>
    public string? Quantity { get; set; }

    public int DisplayOrder { get; set; }
}

/// <summary>
/// One step in a puja, ordered by <see cref="StepNumber"/>.
/// </summary>
/// <remarks>
/// The step carries no text of its own — every word a devotee reads lives in
/// <see cref="PujaStepText"/>, one row per language. That keeps the sequence and the wording
/// independent, so adding a language never disturbs the order and renumbering never touches text.
/// </remarks>
public class PujaStep : BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid PujaId { get; set; }
    public Puja? Puja { get; set; }

    /// <summary>1-based position, kept contiguous by the service whenever steps change.</summary>
    public int StepNumber { get; set; }

    public ICollection<PujaStepText> Texts { get; set; } = new List<PujaStepText>();
}

/// <summary>A step's wording in one language. One row per (step, language).</summary>
public class PujaStepText : BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid PujaStepId { get; set; }
    public PujaStep? PujaStep { get; set; }

    public Guid LanguageId { get; set; }
    public Language? Language { get; set; }

    /// <summary>Short heading for the step, e.g. "Light the lamp".</summary>
    public string? Title { get; set; }

    /// <summary>What the devotee actually does, as sanitized HTML.</summary>
    public string? Content { get; set; }
}

/// <summary>
/// Records that one user finished one step. A row exists only for completed steps, so absence
/// means "not done" and undoing is a delete rather than a flag flip.
/// </summary>
public class UserPujaStepProgress : BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }
    public User? User { get; set; }

    /// <summary>Denormalised so a whole puja's progress can be reset in one query.</summary>
    public Guid PujaId { get; set; }

    public Guid PujaStepId { get; set; }
    public PujaStep? PujaStep { get; set; }

    public DateTime CompletedAtUtc { get; set; } = DateTime.UtcNow;
}
