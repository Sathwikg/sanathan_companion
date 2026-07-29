using Sanathana.Companion.Domain.Common;

namespace Sanathana.Companion.Domain.Entities;

/// <summary>
/// One day of Panchangam for one region. Identified by Date + Region (Year is stored
/// alongside for cheap filtering, as requested).
/// </summary>
public class Panchangam : BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public DateOnly Date { get; set; }
    public int Year { get; set; }

    public Guid RegionId { get; set; }
    public Region? Region { get; set; }

    public string DayOfWeek { get; set; } = string.Empty;

    // ---- year / month context ----
    public string? TeluguSamvatsaram { get; set; }
    public string? Ayanam { get; set; }
    public int? SakaSamvatsaram { get; set; }
    public int? VikramaSamvatsaram { get; set; }
    public string? Masam { get; set; }
    public string? Paksham { get; set; }
    public string? Rutuvu { get; set; }

    // ---- solar day (inputs to every kalam below) ----
    public TimeOnly? Sunrise { get; set; }
    public TimeOnly? Sunset { get; set; }

    // ---- daily elements ----
    public string? TithiDetails { get; set; }
    public string? NakshatramDetails { get; set; }

    // ---- auspicious / inauspicious windows ----
    public string? AmruthaKalam { get; set; }
    public string? AbhijitMuhurtham { get; set; }
    public string? Durmuhurtham { get; set; }
    public string? RahuKalam { get; set; }
    public string? Yamagandam { get; set; }
    public string? Varjyam { get; set; }
    public string? Gulika { get; set; }

    public bool IsActive { get; set; } = true;
}
