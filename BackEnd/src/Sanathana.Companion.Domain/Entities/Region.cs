using Sanathana.Companion.Domain.Common;

namespace Sanathana.Companion.Domain.Entities;

/// <summary>Region master record.</summary>
public class Region : BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    /// <summary>Reference latitude used for Panchangam calculations (degrees north).</summary>
    public double? Latitude { get; set; }

    /// <summary>Reference longitude used for Panchangam calculations (degrees east).</summary>
    public double? Longitude { get; set; }

    public bool IsActive { get; set; } = true;
}
