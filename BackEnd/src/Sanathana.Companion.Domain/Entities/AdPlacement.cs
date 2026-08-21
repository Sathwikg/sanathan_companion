using Sanathana.Companion.Domain.Common;

namespace Sanathana.Companion.Domain.Entities;

/// <summary>
/// Whether one form shows ads, and which single format it shows.
/// </summary>
/// <remarks>
/// One row per module, and <see cref="AdFormatId"/> is a single foreign key rather than a
/// collection — "only one type at a time" is therefore a property of the shape, not a rule someone
/// has to remember to validate.
/// <para>
/// The ad unit ids are per platform because AdMob issues a different one for each, and they are not
/// secrets: every ad unit id ships inside the app binary and is visible to anyone who unpacks it.
/// They live here so a placement can be repointed without a store release.
/// </para>
/// </remarks>
public class AdPlacement : BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid MenuModuleId { get; set; }
    public MenuModule? MenuModule { get; set; }

    /// <summary>Whether this form shows an ad at all.</summary>
    public bool IsEnabled { get; set; }

    /// <summary>The one format shown here. Null while the placement is switched off.</summary>
    public Guid? AdFormatId { get; set; }
    public AdFormat? AdFormat { get; set; }

    public string? AndroidAdUnitId { get; set; }
    public string? IosAdUnitId { get; set; }
}
