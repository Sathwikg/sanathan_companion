using System.ComponentModel.DataAnnotations;

namespace App.Core.Models;

/// <summary>One month of the Hindu calendar for one region, as GET calendar/month returns it.</summary>
public class CalendarMonth
{
    public int Year { get; set; }
    public int Month { get; set; }
    public Guid? RegionId { get; set; }
    public string? RegionName { get; set; }
    public bool HasComputedDays { get; set; }
    public bool MissingPanchangam { get; set; }
    public string? Samvatsaram { get; set; }
    public List<string> Masams { get; set; } = new();
    public List<CalendarDay> Days { get; set; } = new();
    public List<CalendarEventModel> Events { get; set; } = new();
}

public class CalendarDay
{
    public DateOnly Date { get; set; }
    public string DayOfWeek { get; set; } = string.Empty;
    public bool HasPanchangam { get; set; }

    public string? Tithi { get; set; }
    public int? TithiIndex { get; set; }
    public string? Nakshatram { get; set; }
    public int? NakshatraIndex { get; set; }
    public string? Paksham { get; set; }
    public string? Masam { get; set; }
    public string? TithiDetails { get; set; }
    public string? NakshatramDetails { get; set; }

    public TimeOnly? Sunrise { get; set; }
    public TimeOnly? Sunset { get; set; }

    public string? RahuKalam { get; set; }
    public string? Yamagandam { get; set; }
    public string? Gulika { get; set; }
    public string? Durmuhurtham { get; set; }
    public string? Varjyam { get; set; }
    public string? AmruthaKalam { get; set; }
    public string? AbhijitMuhurtham { get; set; }

    /// <summary>new, waxingCrescent, firstQuarter, waxingGibbous, full, waningGibbous, lastQuarter, waningCrescent.</summary>
    public string? MoonPhase { get; set; }
    public int? MoonIllumination { get; set; }

    /// <summary>Monthly observances by tithi, as codes: purnima, amavasya, ekadashi, pradosham …</summary>
    public List<string> Markers { get; set; } = new();

    /// <summary>good, neutral or caution.</summary>
    public string Rating { get; set; } = "neutral";
    public int Score { get; set; }
    public List<string> Reasons { get; set; } = new();

    public List<CalendarFestival> Festivals { get; set; } = new();
    public List<CalendarDeity> Deities { get; set; } = new();

    public bool IsPurnima => TithiIndex == 14;
    public bool IsAmavasya => TithiIndex == 29;
}

public class CalendarFestival
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<CalendarDeity> Deities { get; set; } = new();
}

public class CalendarDeity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string DeityType { get; set; } = "God";
    public bool HasImage { get; set; }
}

/// <summary>A seeker's own mark on the calendar.</summary>
public class CalendarEventModel
{
    public Guid Id { get; set; }
    public DateOnly Date { get; set; }
    public DateOnly OccursOn { get; set; }
    public TimeOnly? Time { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public string Kind { get; set; } = CalendarEventKinds.Personal;
    public bool RepeatYearly { get; set; }
}

public class SaveCalendarEventRequest
{
    public DateOnly Date { get; set; } = DateOnly.FromDateTime(DateTime.Now);

    public TimeOnly? Time { get; set; }

    [Required(ErrorMessage = "Give the mark a title.")]
    [StringLength(120)]
    public string Title { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Notes { get; set; }

    public string Kind { get; set; } = CalendarEventKinds.Personal;

    public bool RepeatYearly { get; set; }
}

/// <summary>The kinds the API accepts, in the order the picker shows them. The server owns the canonical spelling.</summary>
public static class CalendarEventKinds
{
    public const string Puja = "Puja";
    public const string Vratam = "Vratam";
    public const string Festival = "Festival";
    public const string Birthday = "Birthday";
    public const string Anniversary = "Anniversary";
    public const string Shraddha = "Shraddha";
    public const string Personal = "Personal";

    public static readonly string[] All = { Puja, Vratam, Festival, Birthday, Anniversary, Shraddha, Personal };

    /// <summary>A CSS token per kind; never UI text.</summary>
    public static string Slug(string kind) => kind.ToLowerInvariant();
}
