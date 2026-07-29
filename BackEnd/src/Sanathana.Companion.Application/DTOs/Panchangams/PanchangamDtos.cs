namespace Sanathana.Companion.Application.DTOs.Panchangams;

/// <summary>A full day's Panchangam. Used for both stored rows and dynamically computed results.</summary>
public class PanchangamDto
{
    public Guid Id { get; set; }
    public DateOnly Date { get; set; }
    public int Year { get; set; }

    public Guid? RegionId { get; set; }
    public string? RegionName { get; set; }

    /// <summary>Set on dynamically computed results (current location).</summary>
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? PlaceLabel { get; set; }

    /// <summary>True when computed on the fly rather than read from the database.</summary>
    public bool IsComputed { get; set; }

    public string DayOfWeek { get; set; } = string.Empty;
    public string? TeluguSamvatsaram { get; set; }
    public string? Ayanam { get; set; }
    public int? SakaSamvatsaram { get; set; }
    public int? VikramaSamvatsaram { get; set; }
    public string? Masam { get; set; }
    public string? Paksham { get; set; }
    public string? Rutuvu { get; set; }
    public TimeOnly? Sunrise { get; set; }
    public TimeOnly? Sunset { get; set; }
    public string? TithiDetails { get; set; }
    public string? NakshatramDetails { get; set; }
    public string? AmruthaKalam { get; set; }
    public string? AbhijitMuhurtham { get; set; }
    public string? Durmuhurtham { get; set; }
    public string? RahuKalam { get; set; }
    public string? Yamagandam { get; set; }
    public string? Varjyam { get; set; }
    public string? Gulika { get; set; }

    public bool IsActive { get; set; } = true;
}

/// <summary>Request to (re)generate stored Panchangam data for a region across a year.</summary>
public class GeneratePanchangamDto
{
    public int Year { get; set; }

    /// <summary>Null = every active region that has coordinates.</summary>
    public Guid? RegionId { get; set; }

    /// <summary>Recompute and overwrite rows that already exist.</summary>
    public bool Overwrite { get; set; }
}

public class GenerateResultDto
{
    public int Created { get; set; }
    public int Skipped { get; set; }
    public int Updated { get; set; }
    public List<string> Regions { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

public class PanchangamRegionOptionDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public bool HasCoordinates { get; set; }
}
