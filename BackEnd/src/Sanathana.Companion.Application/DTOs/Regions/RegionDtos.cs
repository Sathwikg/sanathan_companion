namespace Sanathana.Companion.Application.DTOs.Regions;

public class RegionDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>Reference coordinates used for Panchangam calculations.</summary>
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    /// <summary>Active languages mapped to this region. Derived from Languages.Regions,
    /// which is the single source of truth for the region ↔ language relationship.</summary>
    public List<Guid> LanguageIds { get; set; } = new();
    public List<string> LanguageNames { get; set; } = new();

    public bool IsActive { get; set; }
}

public class CreateRegionDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    /// <summary>Languages to map to this region.</summary>
    public List<Guid> LanguageIds { get; set; } = new();

    public bool IsActive { get; set; } = true;
}

public class UpdateRegionDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    /// <summary>Languages to map to this region. Replaces the current set of ACTIVE languages;
    /// links held by inactive languages are left untouched.</summary>
    public List<Guid> LanguageIds { get; set; } = new();

    public bool IsActive { get; set; } = true;
}

public class UpdateRegionStatusDto
{
    public bool IsActive { get; set; }
}

/// <summary>Minimal active-region entry for pickers (including the anonymous registration form).</summary>
public class RegionOptionDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
}
