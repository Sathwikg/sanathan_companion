using System.ComponentModel.DataAnnotations;

namespace App.Core.Models;

public class PujaModel
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    public Guid? FestivalId { get; set; }
    public string? FestivalName { get; set; }
    public int? FestivalYear { get; set; }

    public Guid? DeityId { get; set; }
    public string? DeityName { get; set; }

    public bool IsActive { get; set; }

    public bool HasFestival => FestivalId is not null && !string.IsNullOrWhiteSpace(FestivalName);
    public bool HasDeity => DeityId is not null && !string.IsNullOrWhiteSpace(DeityName);

    /// <summary>Festival rows are year-scoped, so the year is part of how one is identified.</summary>
    public string FestivalLabel => FestivalYear is > 0 ? $"{FestivalName} ({FestivalYear})" : FestivalName ?? string.Empty;
}

public class PujaFestivalOption
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Year { get; set; }

    public string Label => Year > 0 ? $"{Name} ({Year})" : Name;
}

public class PujaDeityOption
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class PujaFormOptions
{
    public List<PujaFestivalOption> Festivals { get; set; } = new();
    public List<PujaDeityOption> Deities { get; set; } = new();
}

public class PujaRequest
{
    [Required(ErrorMessage = "Puja name is required.")]
    [StringLength(150)]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    /// <summary>Optional mapping — no [Required], an unset dropdown is a valid answer.</summary>
    public Guid? FestivalId { get; set; }

    /// <summary>Optional mapping.</summary>
    public Guid? DeityId { get; set; }

    public bool IsActive { get; set; } = true;
}
