using Sanathana.Companion.Domain.Common;

namespace Sanathana.Companion.Domain.Entities;

/// <summary>
/// App-wide advertising settings. Exactly one row.
/// </summary>
/// <remarks>
/// A table rather than configuration because the point of it is that an administrator can turn ads
/// off without waiting for a deployment — which is what you want the day a placement misbehaves.
/// </remarks>
public class AdSettings : BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The master switch. False means no form shows an ad, whatever its own setting says.</summary>
    public bool AdsEnabled { get; set; }

    /// <summary>AdMob application id, per platform. Not a secret; it ships in the binary.</summary>
    public string? AndroidAppId { get; set; }
    public string? IosAppId { get; set; }

    /// <summary>
    /// Serve Google's test ad units instead of the real ones.
    /// </summary>
    /// <remarks>
    /// Clicking your own live ads is what gets an AdMob account suspended, so this exists to make
    /// the safe choice the easy one while a placement is being built.
    /// </remarks>
    public bool UseTestAds { get; set; } = true;
}
