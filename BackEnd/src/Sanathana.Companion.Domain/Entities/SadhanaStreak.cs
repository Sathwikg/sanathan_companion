using Sanathana.Companion.Domain.Common;

namespace Sanathana.Companion.Domain.Entities;

/// <summary>
/// A user's practice streak. Advances by one for each consecutive day on which at least one
/// mala is completed; resets to one when a day is missed.
/// </summary>
public class SadhanaStreak : BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }

    public int CurrentStreak { get; set; }
    public int LongestStreak { get; set; }

    /// <summary>The most recent day the user completed a mala.</summary>
    public DateOnly? LastPracticeDate { get; set; }

    /// <summary>Lifetime totals, for the profile display.</summary>
    public int TotalMalas { get; set; }
    public int TotalDaysPracticed { get; set; }
}
