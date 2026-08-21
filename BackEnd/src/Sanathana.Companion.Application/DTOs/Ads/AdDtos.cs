using Sanathana.Companion.Application.Common.Translation;

namespace Sanathana.Companion.Application.DTOs.Ads;

// ---------- master data ----------

/// <summary>One ad format the Google Mobile Ads SDK can serve.</summary>
public class AdFormatDto
{
    public Guid Id { get; set; }

    /// <summary>Stable identifier the clients switch on. Never translated, never displayed.</summary>
    public string Code { get; set; } = string.Empty;

    [Translatable("AdFormat", nameof(Id))]
    public string Name { get; set; } = string.Empty;

    [Translatable("AdFormat", nameof(Id))]
    public string? Description { get; set; }

    [Translatable("AdFormat", nameof(Id))]
    public string? PlacementGuidance { get; set; }

    public bool IsFullScreen { get; set; }
    public bool RequiresUserOptIn { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; }
}

// ---------- the admin config screen ----------

/// <summary>One form, with whatever ad placement it has.</summary>
public class AdModuleDto
{
    public Guid MenuModuleId { get; set; }
    public string ModuleName { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public string? ParentName { get; set; }

    /// <summary>Whether this form shows an ad.</summary>
    public bool IsEnabled { get; set; }

    /// <summary>The single format shown here. Null while it is switched off.</summary>
    public Guid? AdFormatId { get; set; }

    public string? AndroidAdUnitId { get; set; }
    public string? IosAdUnitId { get; set; }
}

/// <summary>Everything the config screen needs in one round trip.</summary>
public class AdConfigDto
{
    public bool AdsEnabled { get; set; }
    public string? AndroidAppId { get; set; }
    public string? IosAppId { get; set; }
    public bool UseTestAds { get; set; }

    public List<AdFormatDto> Formats { get; set; } = new();
    public List<AdModuleDto> Modules { get; set; } = new();
}

// ---------- saving ----------

public class SaveAdModuleDto
{
    public Guid MenuModuleId { get; set; }
    public bool IsEnabled { get; set; }
    public Guid? AdFormatId { get; set; }
    public string? AndroidAdUnitId { get; set; }
    public string? IosAdUnitId { get; set; }
}

public class SaveAdConfigDto
{
    public bool AdsEnabled { get; set; }
    public string? AndroidAppId { get; set; }
    public string? IosAppId { get; set; }
    public bool UseTestAds { get; set; }
    public List<SaveAdModuleDto> Modules { get; set; } = new();
}

// ---------- what a client asks for at run time ----------

/// <summary>
/// The effective ad decision for one form, already resolved for the calling platform.
/// </summary>
/// <remarks>
/// Resolved server-side so a client never has to combine the master switch, the placement and the
/// platform itself — three places to get it wrong, and the wrong answer means an ad in front of
/// somebody who should not have seen one.
/// </remarks>
public class AdSlotDto
{
    public Guid MenuModuleId { get; set; }

    /// <summary>False means show nothing. Every other field is then meaningless.</summary>
    public bool ShowAd { get; set; }

    /// <summary>The format code, e.g. "banner". Null when nothing is shown.</summary>
    public string? FormatCode { get; set; }

    /// <summary>The ad unit for the platform that asked, or Google's test unit in test mode.</summary>
    public string? AdUnitId { get; set; }

    public string? AppId { get; set; }
    public bool IsTestAd { get; set; }
}
