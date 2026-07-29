namespace Sanathana.Companion.Application.DTOs.ChantConfigs;

/// <summary>Row shape for the Chants Config list — deliberately excludes the chant body.</summary>
public class ChantConfigListItemDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    public Guid ChantId { get; set; }
    /// <summary>The chant category name (Ashtakam, Stotra…).</summary>
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

public class ChantConfigDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    public Guid ChantId { get; set; }
    public string ChantName { get; set; } = string.Empty;

    public List<Guid> DeityIds { get; set; } = new();
    public List<string> DeityNames { get; set; } = new();

    /// <summary>Sanitized HTML.</summary>
    public string ChantText { get; set; } = string.Empty;

    public bool HasAudio { get; set; }
    public string? AudioFileName { get; set; }
    public string? AudioContentType { get; set; }
    public long? AudioSizeBytes { get; set; }

    public TimeOnly? FromTime { get; set; }
    public TimeOnly? ToTime { get; set; }
    public string? TimeDescription { get; set; }

    /// <summary>The chant body written per language.</summary>
    public List<ChantLanguageTextDto> LanguageTexts { get; set; } = new();

    public bool IsActive { get; set; }
}

/// <summary>One language's version of a chant's text.</summary>
public class ChantLanguageTextDto
{
    public Guid LanguageId { get; set; }
    public string LanguageName { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
}

public class CreateChantConfigDto
{
    public Guid ChantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<Guid> DeityIds { get; set; } = new();
    public string ChantText { get; set; } = string.Empty;

    /// <summary>Audio as a data URI ("data:audio/mpeg;base64,…"); null = no audio.</summary>
    public string? AudioBase64 { get; set; }
    public string? AudioFileName { get; set; }

    public TimeOnly? FromTime { get; set; }
    public TimeOnly? ToTime { get; set; }
    public string? TimeDescription { get; set; }

    /// <summary>Per-language chant texts. Entries whose text is empty are ignored.</summary>
    public List<ChantLanguageTextDto> LanguageTexts { get; set; } = new();

    public bool IsActive { get; set; } = true;
}

public class UpdateChantConfigDto
{
    public Guid ChantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<Guid> DeityIds { get; set; } = new();
    public string ChantText { get; set; } = string.Empty;

    /// <summary>New audio as a data URI; when null the existing audio is kept unless <see cref="RemoveAudio"/> is set.</summary>
    public string? AudioBase64 { get; set; }
    public string? AudioFileName { get; set; }

    /// <summary>Clears the existing audio (when no new audio is supplied).</summary>
    public bool RemoveAudio { get; set; }

    public TimeOnly? FromTime { get; set; }
    public TimeOnly? ToTime { get; set; }
    public string? TimeDescription { get; set; }

    /// <summary>Per-language chant texts. Replaces the stored set; empty entries clear that language.</summary>
    public List<ChantLanguageTextDto> LanguageTexts { get; set; } = new();

    public bool IsActive { get; set; } = true;
}

public class UpdateChantConfigStatusDto
{
    public bool IsActive { get; set; }
}

public class OptionDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

/// <summary>Options for the Chants Config form's selects.</summary>
public class ChantConfigFormOptionsDto
{
    public List<OptionDto> Categories { get; set; } = new();
    public List<OptionDto> Deities { get; set; } = new();
}
