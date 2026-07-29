using System.ComponentModel.DataAnnotations;

namespace App.Core.Models;

public class LanguageModel
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? NativeName { get; set; }
    public string? Code { get; set; }
    public string? Description { get; set; }
    public List<Guid> RegionIds { get; set; } = new();
    public List<string> RegionNames { get; set; } = new();
    public bool IsActive { get; set; }
}

public class LanguageRequest
{
    [Required(ErrorMessage = "Language name is required.")]
    [StringLength(150)]
    public string Name { get; set; } = string.Empty;

    [StringLength(150)]
    public string? NativeName { get; set; }

    [StringLength(10)]
    public string? Code { get; set; }

    [StringLength(500)]
    public string? Description { get; set; }

    public List<Guid> RegionIds { get; set; } = new();

    public bool IsActive { get; set; } = true;
}

/// <summary>A region together with the languages mapped to it.</summary>
public class RegionLanguagesModel
{
    public Guid RegionId { get; set; }
    public string RegionName { get; set; } = string.Empty;
    public List<string> Languages { get; set; } = new();
}
