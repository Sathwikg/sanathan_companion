using System.ComponentModel.DataAnnotations;

namespace App.Core.Models;

public class DeityModel
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? WelcomeNote { get; set; }
    public string DeityType { get; set; } = "God";
    public bool HasImage { get; set; }
    public List<string> Regions { get; set; } = new();
    public List<string> Festivals { get; set; } = new();
    public List<string> Days { get; set; } = new();
    public bool IsActive { get; set; }
}

public class DeityRequest
{
    [Required(ErrorMessage = "God name is required.")]
    [StringLength(150)]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    [StringLength(1000)]
    public string? WelcomeNote { get; set; }

    public string DeityType { get; set; } = "God";

    /// <summary>New image as a compressed data URI ("data:image/webp;base64,…"); null = unchanged.</summary>
    public string? ImageBase64 { get; set; }

    /// <summary>Clears the existing image on update (when no new image is supplied).</summary>
    public bool RemoveImage { get; set; }

    public List<string> Regions { get; set; } = new();
    public List<string> Festivals { get; set; } = new();
    public List<string> Days { get; set; } = new();
    public bool IsActive { get; set; } = true;
}

public class DeityFormOptions
{
    public List<string> Regions { get; set; } = new();
    public List<string> Festivals { get; set; } = new();
    public List<string> Days { get; set; } = new();
}

/// <summary>Generic value/label option for the MultiSelect component.</summary>
public record SelectOption(string Value, string Label);
