namespace Sanathana.Companion.Application.DTOs.Languages;

public class LanguageDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? NativeName { get; set; }
    public string? Code { get; set; }
    public string? Description { get; set; }

    /// <summary>Region ids this language is mapped to.</summary>
    public List<Guid> RegionIds { get; set; } = new();

    /// <summary>Resolved region names, for display.</summary>
    public List<string> RegionNames { get; set; } = new();

    public bool IsActive { get; set; }
}

public class CreateLanguageDto
{
    public string Name { get; set; } = string.Empty;
    public string? NativeName { get; set; }
    public string? Code { get; set; }
    public string? Description { get; set; }
    public List<Guid> RegionIds { get; set; } = new();
    public bool IsActive { get; set; } = true;
}

public class UpdateLanguageDto
{
    public string Name { get; set; } = string.Empty;
    public string? NativeName { get; set; }
    public string? Code { get; set; }
    public string? Description { get; set; }
    public List<Guid> RegionIds { get; set; } = new();
    public bool IsActive { get; set; } = true;
}

public class UpdateLanguageStatusDto
{
    public bool IsActive { get; set; }
}

/// <summary>A region, plus the languages mapped to it — the region-centric view of the mapping.</summary>
public class RegionLanguagesDto
{
    public Guid RegionId { get; set; }
    public string RegionName { get; set; } = string.Empty;
    public List<string> Languages { get; set; } = new();
}
