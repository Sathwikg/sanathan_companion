namespace App.Core.Models;

public class SadhanaChant
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    /// <summary>Stable grouping key — group on this, not the translated name.</summary>
    public Guid CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public List<string> DeityNames { get; set; } = new();
    public string? TextPreview { get; set; }
    public bool HasAudio { get; set; }
    public int TargetCount { get; set; }
    public string? RecommendReason { get; set; }
    public int TodayCount { get; set; }
    public int TodayMalas { get; set; }
}

public class SadhanaChantDetail
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    /// <summary>Stable grouping key — group on this, not the translated name.</summary>
    public Guid CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public List<string> DeityNames { get; set; } = new();
    /// <summary>The main editor's chant body; the screen shows it only when no language texts exist.</summary>
    public string ChantText { get; set; } = string.Empty;

    /// <summary>The chant in each language it was configured in, every one offered whatever the UI language.</summary>
    public List<SadhanaChantLanguageText> LanguageTexts { get; set; } = new();

    public bool HasAudio { get; set; }
    public string? AudioFileName { get; set; }

    /// <summary>Repetitions in this seeker's mala: their own size when set, else the configured one.</summary>
    public int TargetCount { get; set; }

    /// <summary>The count the Chants master configures for this chant (108 when it sets none).</summary>
    public int DefaultTargetCount { get; set; }

    /// <summary>The seeker's own mala size for this chant; null follows the default.</summary>
    public int? CustomTargetCount { get; set; }

    public bool VibrationEnabled { get; set; } = true;
    public int TodayCount { get; set; }
    public int TodayMalas { get; set; }

    /// <summary>Repetitions so far in the mala in progress.</summary>
    public int CountInMala { get; set; }
}

/// <summary>One language's version of a chant, for the japa screen's language tabs.</summary>
public class SadhanaChantLanguageText
{
    public Guid LanguageId { get; set; }
    public string LanguageName { get; set; } = string.Empty;
    /// <summary>The language's name in its own script — what the tab shows.</summary>
    public string? NativeName { get; set; }
    /// <summary>ISO 639 code, e.g. "te".</summary>
    public string? Code { get; set; }
    public string Text { get; set; } = string.Empty;
}

/// <summary>The seeker's own settings for one chant. A null mala size returns to the default.</summary>
public class SaveChantSettingsRequest
{
    public int? MalaSize { get; set; }
    public bool VibrationEnabled { get; set; } = true;
}

public class SadhanaStreak
{
    public int CurrentStreak { get; set; }
    public int LongestStreak { get; set; }
    public DateOnly? LastPracticeDate { get; set; }
    public int TotalMalas { get; set; }
    public int TotalDaysPracticed { get; set; }
    public bool PracticedToday { get; set; }
}

public class SadhanaSession
{
    public Guid ChantConfigId { get; set; }
    public string ChantName { get; set; } = string.Empty;
    public string? DeityName { get; set; }
    public string? CategoryName { get; set; }
    public int TargetCount { get; set; }
    public int TotalCount { get; set; }
    public int MalasCompleted { get; set; }
    public int CountInMala { get; set; }
}

public class SadhanaToday
{
    public DateOnly Date { get; set; }
    public string DayOfWeek { get; set; } = string.Empty;
    public bool IsFestivalDay { get; set; }
    public string? FestivalName { get; set; }
    public List<SadhanaChant> Recommendations { get; set; } = new();
    public List<SadhanaSession> TodaySessions { get; set; } = new();
    public SadhanaStreak Streak { get; set; } = new();
    public int TodayMalas { get; set; }
    public int TodayChantsPracticed { get; set; }
}

public class LogCountRequest
{
    public Guid ChantConfigId { get; set; }
    public int TotalCount { get; set; }
}

public class LogCountResult
{
    public SadhanaSession Session { get; set; } = new();
    public SadhanaStreak Streak { get; set; } = new();
    public bool MalaCompleted { get; set; }
    public bool StreakSecuredToday { get; set; }
}
