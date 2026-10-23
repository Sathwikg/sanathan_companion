using Sanathana.Companion.Domain.Common;

namespace Sanathana.Companion.Domain.Entities;

/// <summary>
/// One seeker's own settings for one chant on the japa screen: how many repetitions make their
/// mala, and whether the phone vibrates as they count.
/// </summary>
/// <remarks>
/// No row means the defaults: the chant category's configured count (or 108) and vibration on.
/// Like <see cref="UserFavorite"/>, it holds plain ids rather than foreign keys; the account purge
/// removes it with the rest of the seeker's data.
/// </remarks>
public class UserChantSetting : BaseEntity
{
    /// <summary>The smallest mala a seeker may set. Fewer than three is not a mala.</summary>
    public const int MinMalaSize = 3;

    /// <summary>A ceiling that keeps a typo from making a mala that can never complete.</summary>
    public const int MaxMalaSize = 100_000;

    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }

    public Guid ChantConfigId { get; set; }

    /// <summary>Repetitions in one of this seeker's malas; null follows the chant's configured count.</summary>
    public int? MalaSize { get; set; }

    public bool VibrationEnabled { get; set; } = true;
}
