using Sanathana.Companion.Domain.Common;

namespace Sanathana.Companion.Domain.Entities;

/// <summary>
/// One user's sadhana with one chant on one day. Holds the running japa count and how many
/// full malas that represents. A "mala" completes when the count reaches the chant's target.
/// </summary>
public class SadhanaLog : BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }
    public DateOnly Date { get; set; }

    public Guid ChantConfigId { get; set; }
    public ChantConfig? ChantConfig { get; set; }

    /// <summary>Denormalised for cheap history display without re-joining.</summary>
    public string ChantName { get; set; } = string.Empty;
    public string? DeityName { get; set; }
    public string? CategoryName { get; set; }

    /// <summary>Repetitions that make one mala (the chant category's count, or 108 by default).</summary>
    public int TargetCount { get; set; } = 108;

    /// <summary>Total repetitions today for this chant.</summary>
    public int TotalCount { get; set; }

    /// <summary>Completed malas = TotalCount / TargetCount.</summary>
    public int MalasCompleted { get; set; }

    /// <summary>True when this chant was in the day's recommendations.</summary>
    public bool WasRecommended { get; set; }
}
