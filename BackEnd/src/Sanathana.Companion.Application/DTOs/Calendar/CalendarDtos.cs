using Sanathana.Companion.Application.Common.Translation;

namespace Sanathana.Companion.Application.DTOs.Calendar;

/// <summary>One month of the Hindu calendar for one region, ready to draw as a grid.</summary>
public class CalendarMonthDto
{
    public int Year { get; set; }
    public int Month { get; set; }

    public Guid? RegionId { get; set; }
    [Translatable("Region", nameof(RegionId))]
    public string? RegionName { get; set; }

    /// <summary>True when some days had no stored Panchangam and were computed on the fly.</summary>
    public bool HasComputedDays { get; set; }

    /// <summary>True when no Panchangam at all could be had (a region without coordinates).</summary>
    public bool MissingPanchangam { get; set; }

    [Translatable(Category = "panchangam")]
    public string? Samvatsaram { get; set; }

    /// <summary>The lunar months the civil month touches, in order — usually two.</summary>
    [Translatable(Category = "panchangam")]
    public List<string> Masams { get; set; } = new();

    public List<CalendarDayDto> Days { get; set; } = new();

    /// <summary>The caller's own marks falling in the month; empty for a guest.</summary>
    public List<CalendarEventDto> Events { get; set; } = new();
}

public class CalendarDayDto
{
    public DateOnly Date { get; set; }
    public string DayOfWeek { get; set; } = string.Empty;

    public bool HasPanchangam { get; set; }

    /// <summary>The tithi at sunrise, as a name; null when unknown.</summary>
    [Translatable(Category = "panchangam")]
    public string? Tithi { get; set; }
    public int? TithiIndex { get; set; }

    [Translatable(Category = "panchangam")]
    public string? Nakshatram { get; set; }
    public int? NakshatraIndex { get; set; }

    [Translatable(Category = "panchangam")]
    public string? Paksham { get; set; }
    [Translatable(Category = "panchangam")]
    public string? Masam { get; set; }

    [Translatable(Composite = true, Category = "panchangam")]
    public string? TithiDetails { get; set; }
    [Translatable(Composite = true, Category = "panchangam")]
    public string? NakshatramDetails { get; set; }

    public TimeOnly? Sunrise { get; set; }
    public TimeOnly? Sunset { get; set; }

    [Translatable(Composite = true, Category = "panchangam")]
    public string? RahuKalam { get; set; }
    [Translatable(Composite = true, Category = "panchangam")]
    public string? Yamagandam { get; set; }
    [Translatable(Composite = true, Category = "panchangam")]
    public string? Gulika { get; set; }
    [Translatable(Composite = true, Category = "panchangam")]
    public string? Durmuhurtham { get; set; }
    [Translatable(Composite = true, Category = "panchangam")]
    public string? Varjyam { get; set; }
    [Translatable(Composite = true, Category = "panchangam")]
    public string? AmruthaKalam { get; set; }
    [Translatable(Composite = true, Category = "panchangam")]
    public string? AbhijitMuhurtham { get; set; }

    /// <summary>Phase code — see CalendarRules.MoonPhase — and the lit fraction, 0..100.</summary>
    public string? MoonPhase { get; set; }
    public int? MoonIllumination { get; set; }

    /// <summary>Monthly observances by tithi (purnima, ekadashi, pradosham …), as codes.</summary>
    public List<string> Markers { get; set; } = new();

    /// <summary>good / neutral / caution, the score behind it, and the reason codes.</summary>
    public string Rating { get; set; } = "neutral";
    public int Score { get; set; }
    public List<string> Reasons { get; set; } = new();

    public List<CalendarFestivalDto> Festivals { get; set; } = new();

    /// <summary>The deities worshipped on this weekday in the region.</summary>
    public List<CalendarDeityDto> Deities { get; set; } = new();
}

public class CalendarFestivalDto
{
    public Guid Id { get; set; }
    [Translatable("Festival", nameof(Id))]
    public string Name { get; set; } = string.Empty;
    [Translatable("Festival", nameof(Id), Field = "Description")]
    public string? Description { get; set; }
    public List<CalendarDeityDto> Deities { get; set; } = new();
}

public class CalendarDeityDto
{
    public Guid Id { get; set; }
    [Translatable("Deity", nameof(Id))]
    public string Name { get; set; } = string.Empty;
    public string DeityType { get; set; } = "God";
    public bool HasImage { get; set; }
}

/// <summary>A seeker's own mark. Personal text: never walked by the translator.</summary>
[NoTranslate]
public class CalendarEventDto
{
    public Guid Id { get; set; }

    /// <summary>The stored date — the first occurrence of a yearly repeat.</summary>
    public DateOnly Date { get; set; }

    /// <summary>The date it falls on inside the month asked for (differs from Date for a repeat).</summary>
    public DateOnly OccursOn { get; set; }

    public TimeOnly? Time { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public string Kind { get; set; } = string.Empty;
    public bool RepeatYearly { get; set; }
}

[NoTranslate]
public class SaveCalendarEventDto
{
    public DateOnly Date { get; set; }
    public TimeOnly? Time { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public string Kind { get; set; } = "Personal";
    public bool RepeatYearly { get; set; }
}
