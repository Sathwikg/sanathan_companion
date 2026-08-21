namespace App.Core.Models;

/// <summary>One ad format the Google Mobile Ads SDK can serve (master data).</summary>
public class AdFormatModel
{
    public Guid Id { get; set; }

    /// <summary>Stable identifier the apps switch on, e.g. "banner". Never displayed.</summary>
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>Google's own placement rule, shown when this format is chosen.</summary>
    public string? PlacementGuidance { get; set; }

    /// <summary>Takes over the whole screen. These are the ones that interrupt.</summary>
    public bool IsFullScreen { get; set; }

    public bool RequiresUserOptIn { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; }
}

/// <summary>One form and whatever ad placement it has.</summary>
public class AdModuleModel
{
    public Guid MenuModuleId { get; set; }
    public string ModuleName { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public string? ParentName { get; set; }

    public bool IsEnabled { get; set; }

    /// <summary>The single format shown here — one, never several.</summary>
    public Guid? AdFormatId { get; set; }

    public string? AndroidAdUnitId { get; set; }
    public string? IosAdUnitId { get; set; }
}

public class AdConfigModel
{
    public bool AdsEnabled { get; set; }
    public string? AndroidAppId { get; set; }
    public string? IosAppId { get; set; }
    public bool UseTestAds { get; set; } = true;

    public List<AdFormatModel> Formats { get; set; } = new();
    public List<AdModuleModel> Modules { get; set; } = new();
}

public class SaveAdModuleRequest
{
    public Guid MenuModuleId { get; set; }
    public bool IsEnabled { get; set; }
    public Guid? AdFormatId { get; set; }
    public string? AndroidAdUnitId { get; set; }
    public string? IosAdUnitId { get; set; }
}

public class SaveAdConfigRequest
{
    public bool AdsEnabled { get; set; }
    public string? AndroidAppId { get; set; }
    public string? IosAppId { get; set; }
    public bool UseTestAds { get; set; }
    public List<SaveAdModuleRequest> Modules { get; set; } = new();
}

/// <summary>The resolved ad decision for one form on one platform.</summary>
public class AdSlotModel
{
    public Guid MenuModuleId { get; set; }
    public bool ShowAd { get; set; }
    public string? FormatCode { get; set; }
    public string? AdUnitId { get; set; }
    public string? AppId { get; set; }
    public bool IsTestAd { get; set; }
}
