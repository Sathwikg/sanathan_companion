using Sanathana.Companion.Application.Common.Translation;

namespace Sanathana.Companion.Application.DTOs.Pujas;

public class PujaDto
{
    public Guid Id { get; set; }

    /// <summary>
    /// Not translated. The master list's edit modal is populated from this same object, so a
    /// translated name would be written straight back to the row on save.
    /// </summary>
    [NoTranslate]
    public string Name { get; set; } = string.Empty;

    [NoTranslate]
    public string? Description { get; set; }

    /// <summary>Optional mapping.</summary>
    public Guid? FestivalId { get; set; }

    /// <summary>Display only — the form binds <see cref="FestivalId"/>, never this.</summary>
    [Translatable(Category = "festival")]
    public string? FestivalName { get; set; }

    public int? FestivalYear { get; set; }

    /// <summary>Optional mapping.</summary>
    public Guid? DeityId { get; set; }

    /// <summary>Display only — the form binds <see cref="DeityId"/>, never this.</summary>
    [Translatable(Category = "deity")]
    public string? DeityName { get; set; }

    public bool IsActive { get; set; }
}

/// <summary>Options are id + label, so no name is ever posted back as an identifier.</summary>
public class PujaFestivalOptionDto
{
    public Guid Id { get; set; }

    [Translatable("Festival", nameof(Id))]
    public string Name { get; set; } = string.Empty;

    public int Year { get; set; }
}

public class PujaDeityOptionDto
{
    public Guid Id { get; set; }

    [Translatable("Deity", nameof(Id))]
    public string Name { get; set; } = string.Empty;
}

public class PujaFormOptionsDto
{
    public List<PujaFestivalOptionDto> Festivals { get; set; } = new();
    public List<PujaDeityOptionDto> Deities { get; set; } = new();
}

public class CreatePujaDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>Optional — null means the puja is not tied to a festival.</summary>
    public Guid? FestivalId { get; set; }

    /// <summary>Optional — null means the puja is not tied to a deity.</summary>
    public Guid? DeityId { get; set; }

    public bool IsActive { get; set; } = true;
}

public class UpdatePujaDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? FestivalId { get; set; }
    public Guid? DeityId { get; set; }
    public bool IsActive { get; set; } = true;
}
