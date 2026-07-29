namespace App.Core.Models;

public class UserListItem
{
    public Guid UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? SeekerName { get; set; }
    public string Email { get; set; } = string.Empty;
    public string MobileNumber { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
    public DateTime RegisteredOn { get; set; }
    public int CurrentStreak { get; set; }
    public int TotalMalas { get; set; }
}

public class UserProfile
{
    public Guid UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? SeekerName { get; set; }
    public string Email { get; set; } = string.Empty;
    public string MobileNumber { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
    public DateTime RegisteredOn { get; set; }
    public DateTime? LastUpdatedOn { get; set; }
    public int CurrentStreak { get; set; }
    public int LongestStreak { get; set; }
    public int TotalMalas { get; set; }
    public int TotalDaysPracticed { get; set; }
    public DateOnly? LastPracticeDate { get; set; }
    public List<UserSadhanaEntry> RecentSadhana { get; set; } = new();
}

public class UserSadhanaEntry
{
    public DateOnly Date { get; set; }
    public string ChantName { get; set; } = string.Empty;
    public string? DeityName { get; set; }
    public string? CategoryName { get; set; }
    public int MalasCompleted { get; set; }
    public int TotalCount { get; set; }
}

/// <summary>The signed-in user's own profile, with a per-day sadhana timeline.</summary>
public class MyProfile
{
    public Guid UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? SeekerName { get; set; }
    public string Email { get; set; } = string.Empty;
    public string MobileNumber { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
    public DateTime RegisteredOn { get; set; }
    public DateTime? LastUpdatedOn { get; set; }

    /// <summary>The user's preferred region; seeds the app's region selector.</summary>
    public Guid? DefaultRegionId { get; set; }
    public string? DefaultRegionName { get; set; }

    public int CurrentStreak { get; set; }
    public int LongestStreak { get; set; }
    public int TotalMalas { get; set; }
    public int TotalDaysPracticed { get; set; }
    public DateOnly? LastPracticeDate { get; set; }
    public bool PracticedToday { get; set; }

    public List<SadhanaDay> Timeline { get; set; } = new();
}

public class SadhanaDay
{
    public DateOnly Date { get; set; }
    public int TotalMalas { get; set; }
    public int TotalCount { get; set; }
    public List<UserSadhanaEntry> Entries { get; set; } = new();
}
