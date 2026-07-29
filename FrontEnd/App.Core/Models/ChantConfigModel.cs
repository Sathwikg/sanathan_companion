using System.ComponentModel.DataAnnotations;

namespace App.Core.Models;

/// <summary>Row shape for the Chants Config list (no chant body).</summary>
public class ChantConfigListItem
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid ChantId { get; set; }
    public string ChantName { get; set; } = string.Empty;
    public List<Guid> DeityIds { get; set; } = new();
    public List<string> DeityNames { get; set; } = new();
    /// <summary>Short plain-text snippet of the chant body, for card previews.</summary>
    public string TextPreview { get; set; } = string.Empty;
    public bool HasAudio { get; set; }
    public TimeOnly? FromTime { get; set; }
    public TimeOnly? ToTime { get; set; }
    public string? TimeDescription { get; set; }
    public bool IsActive { get; set; }
}

public class ChantConfigModel
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid ChantId { get; set; }
    public string ChantName { get; set; } = string.Empty;
    public List<Guid> DeityIds { get; set; } = new();
    public List<string> DeityNames { get; set; } = new();
    public string ChantText { get; set; } = string.Empty;
    public bool HasAudio { get; set; }
    public string? AudioFileName { get; set; }
    public string? AudioContentType { get; set; }
    public long? AudioSizeBytes { get; set; }
    public TimeOnly? FromTime { get; set; }
    public TimeOnly? ToTime { get; set; }
    public string? TimeDescription { get; set; }
    public List<ChantLanguageText> LanguageTexts { get; set; } = new();
    public bool IsActive { get; set; }
}

public class ChantLanguageText
{
    public Guid LanguageId { get; set; }
    public string LanguageName { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
}

public class ChantConfigRequest
{
    public Guid ChantId { get; set; }

    [Required(ErrorMessage = "Chant name is required.")]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    public List<Guid> DeityIds { get; set; } = new();

    public string ChantText { get; set; } = string.Empty;

    /// <summary>New audio as a data URI; null keeps the existing audio.</summary>
    public string? AudioBase64 { get; set; }
    public string? AudioFileName { get; set; }
    public bool RemoveAudio { get; set; }

    public TimeOnly? FromTime { get; set; }
    public TimeOnly? ToTime { get; set; }

    [StringLength(200)]
    public string? TimeDescription { get; set; }

    /// <summary>Per-language chant texts (LanguageId + HTML).</summary>
    public List<ChantLanguageText> LanguageTexts { get; set; } = new();

    public bool IsActive { get; set; } = true;
}

public class ChantConfigOption
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class ChantConfigFormOptions
{
    public List<ChantConfigOption> Categories { get; set; } = new();
    public List<ChantConfigOption> Deities { get; set; } = new();
}
