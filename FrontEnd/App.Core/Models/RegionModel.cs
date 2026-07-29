using System.ComponentModel.DataAnnotations;

namespace App.Core.Models;

public class RegionModel
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>Reference coordinates used for Panchangam calculations.</summary>
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    /// <summary>Active languages mapped to this region (derived from the Languages master).</summary>
    public List<Guid> LanguageIds { get; set; } = new();
    public List<string> LanguageNames { get; set; } = new();

    public bool IsActive { get; set; }
}

/// <summary>Minimal active-region entry used by the region pickers (and the registration form).</summary>
public class RegionOption
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class RegionRequest
{
    [Required(ErrorMessage = "Region name is required.")]
    [StringLength(150)]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }

    [Range(-90, 90, ErrorMessage = "Latitude must be between -90 and 90.")]
    public double? Latitude { get; set; }

    [Range(-180, 180, ErrorMessage = "Longitude must be between -180 and 180.")]
    public double? Longitude { get; set; }

    public List<Guid> LanguageIds { get; set; } = new();

    public bool IsActive { get; set; } = true;
}
