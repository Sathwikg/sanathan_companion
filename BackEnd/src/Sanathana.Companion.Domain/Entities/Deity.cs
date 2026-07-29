using Sanathana.Companion.Domain.Common;

namespace Sanathana.Companion.Domain.Entities;

/// <summary>Deity / God master record.</summary>
public class Deity : BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? WelcomeNote { get; set; }

    /// <summary>"God" or "Goddess".</summary>
    public string DeityType { get; set; } = "God";

    /// <summary>Compressed profile picture stored as a binary blob (bytea).</summary>
    public byte[]? ImageData { get; set; }
    public string? ImageContentType { get; set; }

    /// <summary>Comma-separated region names.</summary>
    public string? Regions { get; set; }

    /// <summary>Comma-separated festival names.</summary>
    public string? Festivals { get; set; }

    /// <summary>Comma-separated day names.</summary>
    public string? Days { get; set; }

    public bool IsActive { get; set; } = true;
}
