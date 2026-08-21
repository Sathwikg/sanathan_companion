using Sanathana.Companion.Domain.Common;

namespace Sanathana.Companion.Domain.Entities;

/// <summary>
/// One of the ad formats the Google Mobile Ads SDK serves (master data).
/// </summary>
/// <remarks>
/// The six rows are defined by Google, not by this business, so they are seeded and not creatable
/// from the admin form — inventing a seventh would produce a placement the SDK cannot render. An
/// administrator can still deactivate one they never want offered.
/// <para>
/// <see cref="PlacementGuidance"/> carries Google's own rule for the format. It is shown beside the
/// choice on the config screen deliberately: several of these formats are policy violations if put
/// in the wrong place, and the moment somebody picks one is the moment they should read why.
/// </para>
/// </remarks>
public class AdFormat : BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Stable identifier the clients switch on, e.g. "interstitial". Never displayed.</summary>
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary>How it appears to the seeker.</summary>
    public string? Description { get; set; }

    /// <summary>Google's placement rule for this format, shown when it is chosen.</summary>
    public string? PlacementGuidance { get; set; }

    /// <summary>Takes over the whole screen. These are the ones that interrupt.</summary>
    public bool IsFullScreen { get; set; }

    /// <summary>The seeker must choose to watch it before it plays.</summary>
    public bool RequiresUserOptIn { get; set; }

    public int DisplayOrder { get; set; }

    public bool IsActive { get; set; } = true;
}
