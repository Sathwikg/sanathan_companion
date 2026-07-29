namespace Sanathana.Companion.Application.DTOs.Sadhana;

/// <summary>A chant as it appears in the Sadhana list, with the current user's progress today.</summary>
public class SadhanaChantDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public List<string> DeityNames { get; set; } = new();
    public string? TextPreview { get; set; }
    public bool HasAudio { get; set; }

    /// <summary>Repetitions that make one mala.</summary>
    public int TargetCount { get; set; }

    /// <summary>Why this chant is recommended today (e.g. "Tuesday · Hanuman" or "Diwali · Lakshmi").</summary>
    public string? RecommendReason { get; set; }

    // the user's progress today
    public int TodayCount { get; set; }
    public int TodayMalas { get; set; }
}

/// <summary>Full chant detail for the japa screen.</summary>
public class SadhanaChantDetailDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public List<string> DeityNames { get; set; } = new();

    /// <summary>Sanitized HTML chant body.</summary>
    public string ChantText { get; set; } = string.Empty;

    public bool HasAudio { get; set; }
    public string? AudioFileName { get; set; }

    public int TargetCount { get; set; }
    public int TodayCount { get; set; }
    public int TodayMalas { get; set; }
}

public class SadhanaStreakDto
{
    public int CurrentStreak { get; set; }
    public int LongestStreak { get; set; }
    public DateOnly? LastPracticeDate { get; set; }
    public int TotalMalas { get; set; }
    public int TotalDaysPracticed { get; set; }

    /// <summary>True once at least one mala has been completed today.</summary>
    public bool PracticedToday { get; set; }
}

/// <summary>A chant the user has already done today.</summary>
public class SadhanaSessionDto
{
    public Guid ChantConfigId { get; set; }
    public string ChantName { get; set; } = string.Empty;
    public string? DeityName { get; set; }
    public string? CategoryName { get; set; }
    public int TargetCount { get; set; }
    public int TotalCount { get; set; }
    public int MalasCompleted { get; set; }
}

public class SadhanaTodayDto
{
    public DateOnly Date { get; set; }
    public string DayOfWeek { get; set; } = string.Empty;

    /// <summary>Set when a festival falls today, in which case recommendations are festival-driven.</summary>
    public bool IsFestivalDay { get; set; }
    public string? FestivalName { get; set; }

    public List<SadhanaChantDto> Recommendations { get; set; } = new();
    public List<SadhanaSessionDto> TodaySessions { get; set; } = new();
    public SadhanaStreakDto Streak { get; set; } = new();

    public int TodayMalas { get; set; }
    public int TodayChantsPracticed { get; set; }
}

/// <summary>Sets the absolute japa total for a chant today (idempotent — safe to re-send).</summary>
public class LogCountDto
{
    public Guid ChantConfigId { get; set; }
    public int TotalCount { get; set; }
}

public class LogCountResultDto
{
    public SadhanaSessionDto Session { get; set; } = new();
    public SadhanaStreakDto Streak { get; set; } = new();

    /// <summary>True when this update pushed the user across a new mala boundary.</summary>
    public bool MalaCompleted { get; set; }

    /// <summary>True when this update was the first mala of the day (streak secured).</summary>
    public bool StreakSecuredToday { get; set; }
}
