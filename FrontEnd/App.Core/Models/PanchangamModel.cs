namespace App.Core.Models;

public class PanchangamModel
{
    public Guid Id { get; set; }
    public DateOnly Date { get; set; }
    public int Year { get; set; }

    public Guid? RegionId { get; set; }
    public string? RegionName { get; set; }

    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? PlaceLabel { get; set; }
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

public class PanchangamRegionOption
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public bool HasCoordinates { get; set; }
}

public class PanchangamOptions
{
    public List<int> Years { get; set; } = new();
    public List<PanchangamRegionOption> Regions { get; set; } = new();
}

public class GeneratePanchangamRequest
{
    public int Year { get; set; }
    public Guid? RegionId { get; set; }
    public bool Overwrite { get; set; }
}

public class GenerateResult
{
    public int Created { get; set; }
    public int Skipped { get; set; }
    public int Updated { get; set; }
    public List<string> Regions { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

/// <summary>Browser geolocation result.</summary>
public class GeoPosition
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double Accuracy { get; set; }
    public string? Error { get; set; }
}
