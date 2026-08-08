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

    public Guid FestivalId { get; set; }

    /// <summary>Display only — the form binds <see cref="FestivalId"/>, never this.</summary>
    [Translatable("Festival", nameof(FestivalId))]
    public string FestivalName { get; set; } = string.Empty;

    public int FestivalYear { get; set; }
    public DateOnly FestivalDate { get; set; }
    public bool IsActive { get; set; }
}

/// <summary>Festivals for the form's dropdown, as id + label so no name is ever posted back.</summary>
public class PujaFestivalOptionDto
{
    public Guid Id { get; set; }

    [Translatable("Festival", nameof(Id))]
    public string Name { get; set; } = string.Empty;

    public int Year { get; set; }
}

public class PujaFormOptionsDto
{
    public List<PujaFestivalOptionDto> Festivals { get; set; } = new();
}

public class CreatePujaDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid FestivalId { get; set; }
    public bool IsActive { get; set; } = true;
}

public class UpdatePujaDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid FestivalId { get; set; }
    public bool IsActive { get; set; } = true;
}
