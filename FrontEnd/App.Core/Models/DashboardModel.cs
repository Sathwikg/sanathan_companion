namespace App.Core.Models;

public class DashboardModel
{
    public string FullName { get; set; } = string.Empty;
    public string? SeekerName { get; set; }
    public string Role { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

/// <summary>Administrator overview: community size and sadhana engagement across all seekers.</summary>
public class AdminDashboardModel
{
    public int TotalUsers { get; set; }
    public int TotalSeekers { get; set; }
    public int TotalAdmins { get; set; }
    public int NewThisWeek { get; set; }

    public int TotalMalas { get; set; }
    public long TotalJapa { get; set; }
    public int TotalSessions { get; set; }
    public int ActiveToday { get; set; }
    public int TotalDaysPracticed { get; set; }
    public int LongestStreak { get; set; }

    public int Deities { get; set; }
    public int Chants { get; set; }
    public int Festivals { get; set; }
    public int Regions { get; set; }

    public DateTime GeneratedAtUtc { get; set; }
}

/// <summary>"Today's Bhakti": today's deity/deities and the sadhana configured for each.</summary>
public class TodayBhakti
{
    public DateOnly Date { get; set; }
    public string DayOfWeek { get; set; } = string.Empty;
    public bool IsFestivalDay { get; set; }
    public string? FestivalName { get; set; }
    public List<TodayDeity> Deities { get; set; } = new();
}

public class TodayDeity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string DeityType { get; set; } = "God";
    public string? Description { get; set; }
    public string? WelcomeNote { get; set; }
    public bool HasImage { get; set; }
    public List<string> Days { get; set; } = new();
    public string? Reason { get; set; }
    public List<TodaySadhana> Sadhanas { get; set; } = new();
}

public class TodaySadhana
{
    public Guid ChantConfigId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public bool HasAudio { get; set; }
}

/// <summary>Time-configured prayers ranked by relevance to the current time.</summary>
public class PrayersResult
{
    public TimeOnly CurrentTime { get; set; }
    public List<Prayer> Prayers { get; set; } = new();
}

public class Prayer
{
    public Guid ChantConfigId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public List<string> DeityNames { get; set; } = new();

    public TimeOnly? FromTime { get; set; }
    public TimeOnly? ToTime { get; set; }
    public string? TimeDescription { get; set; }

    /// <summary>Morning, Food, Afternoon, Evening, Night or Anytime — drives the icon.</summary>
    public string Slot { get; set; } = "Anytime";
    public bool IsActiveNow { get; set; }
}
