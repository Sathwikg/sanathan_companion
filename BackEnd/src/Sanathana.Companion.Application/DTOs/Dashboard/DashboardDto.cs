namespace Sanathana.Companion.Application.DTOs.Dashboard;

public class DashboardDto
{
    public string FullName { get; set; } = string.Empty;
    public string? SeekerName { get; set; }
    public string Role { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

/// <summary>Administrator overview: community size and sadhana engagement across all seekers.</summary>
public class AdminDashboardDto
{
    // Community / onboarding
    public int TotalUsers { get; set; }
    public int TotalSeekers { get; set; }
    public int TotalAdmins { get; set; }
    public int NewThisWeek { get; set; }

    // Sadhana engagement (all seekers combined)
    public int TotalMalas { get; set; }
    public long TotalJapa { get; set; }
    public int TotalSessions { get; set; }
    public int ActiveToday { get; set; }
    public int TotalDaysPracticed { get; set; }
    public int LongestStreak { get; set; }

    // Catalog / content configured
    public int Deities { get; set; }
    public int Chants { get; set; }
    public int Festivals { get; set; }
    public int Regions { get; set; }

    public DateTime GeneratedAtUtc { get; set; }
}

/// <summary>"Today's Bhakti" for the user dashboard: today's deity/deities (by weekday or festival)
/// and the sadhana (chant) configured for each.</summary>
public class TodayBhaktiDto
{
    public DateOnly Date { get; set; }
    public string DayOfWeek { get; set; } = string.Empty;
    public bool IsFestivalDay { get; set; }
    public string? FestivalName { get; set; }
    public List<TodayDeityDto> Deities { get; set; } = new();
}

public class TodayDeityDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string DeityType { get; set; } = "God";
    public string? Description { get; set; }
    public string? WelcomeNote { get; set; }
    public bool HasImage { get; set; }
    public List<string> Days { get; set; } = new();

    /// <summary>Why this deity is today's focus, e.g. "Sunday" or "Festival · Diwali".</summary>
    public string? Reason { get; set; }

    /// <summary>The chant(s) configured for this deity — each opens its japa screen.</summary>
    public List<TodaySadhanaDto> Sadhanas { get; set; } = new();
}

public class TodaySadhanaDto
{
    public Guid ChantConfigId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public bool HasAudio { get; set; }
}

/// <summary>Time-configured prayers for the dashboard, ranked by relevance to the current time.</summary>
public class PrayersDto
{
    /// <summary>The current time (IST) the relevance was computed against.</summary>
    public TimeOnly CurrentTime { get; set; }
    public List<PrayerDto> Prayers { get; set; } = new();
}

public class PrayerDto
{
    public Guid ChantConfigId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public List<string> DeityNames { get; set; } = new();

    public TimeOnly? FromTime { get; set; }
    public TimeOnly? ToTime { get; set; }
    public string? TimeDescription { get; set; }

    /// <summary>Time-of-day bucket driving the icon: Morning, Food, Afternoon, Evening, Night or Anytime.</summary>
    public string Slot { get; set; } = "Anytime";

    /// <summary>True when the current time falls within this prayer's configured window.</summary>
    public bool IsActiveNow { get; set; }
}
