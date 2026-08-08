using System.ComponentModel.DataAnnotations;

namespace App.Core.Models;

public class PujaModel
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    public Guid FestivalId { get; set; }
    public string FestivalName { get; set; } = string.Empty;
    public int FestivalYear { get; set; }
    public DateOnly FestivalDate { get; set; }

    public bool IsActive { get; set; }

    /// <summary>Festival rows are year-scoped, so the year is part of how one is identified.</summary>
    public string FestivalLabel => FestivalYear > 0 ? $"{FestivalName} ({FestivalYear})" : FestivalName;
}

public class PujaFestivalOption
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Year { get; set; }

    public string Label => Year > 0 ? $"{Name} ({Year})" : Name;
}

public class PujaFormOptions
{
    public List<PujaFestivalOption> Festivals { get; set; } = new();
}

public class PujaRequest
{
    [Required(ErrorMessage = "Puja name is required.")]
    [StringLength(150)]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    // Nullable on purpose: [Required] on a plain Guid never fails, because Guid.Empty is not null.
    // The server rejects an empty id as well, so this is the friendly half of a two-sided check.
    [Required(ErrorMessage = "Choose the festival this puja belongs to.")]
    public Guid? FestivalId { get; set; }

    public bool IsActive { get; set; } = true;
}
