namespace Sanathana.Companion.Application.DTOs.Deities;

public class DeityDto
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

public class CreateDeityDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? WelcomeNote { get; set; }
    public string DeityType { get; set; } = "God";

    /// <summary>New image as a data URI ("data:image/webp;base64,…"); null = no image.</summary>
    public string? ImageBase64 { get; set; }

    public List<string> Regions { get; set; } = new();
    public List<string> Festivals { get; set; } = new();
    public List<string> Days { get; set; } = new();
    public bool IsActive { get; set; } = true;
}

public class UpdateDeityDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? WelcomeNote { get; set; }
    public string DeityType { get; set; } = "God";

    /// <summary>New image as a data URI; when null, the existing image is kept unless <see cref="RemoveImage"/> is set.</summary>
    public string? ImageBase64 { get; set; }

    /// <summary>Clears the existing image (when no new image is supplied).</summary>
    public bool RemoveImage { get; set; }

    public List<string> Regions { get; set; } = new();
    public List<string> Festivals { get; set; } = new();
    public List<string> Days { get; set; } = new();
    public bool IsActive { get; set; } = true;
}

public class UpdateDeityStatusDto
{
    public bool IsActive { get; set; }
}

/// <summary>Options for the deity form's multi-selects (all as name lists).</summary>
public class DeityFormOptionsDto
{
    public List<string> Regions { get; set; } = new();
    public List<string> Festivals { get; set; } = new();
    public List<string> Days { get; set; } = new();
}
