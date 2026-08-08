namespace Sanathana.Companion.Application.DTOs.Users;

/// <summary>A registered user for the User master list. Never carries the password hash.</summary>
public class UserListItemDto
{
    public Guid UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? SeekerName { get; set; }
    public string Email { get; set; } = string.Empty;
    public string MobileNumber { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;

    /// <summary>
    /// Role-independent of the display text. UI styling and permission checks must use this,
    /// never <see cref="RoleName"/>, because the role name is translated for display.
    /// </summary>
    public bool IsAdmin { get; set; }
    public DateTime RegisteredOn { get; set; }

    // a glance at their practice
    public int CurrentStreak { get; set; }
    public int TotalMalas { get; set; }
}

/// <summary>Full profile of one user, including their sadhana practice. No password hash.</summary>
public class UserProfileDto
{
    public Guid UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? SeekerName { get; set; }
    public string Email { get; set; } = string.Empty;
    public string MobileNumber { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;

    /// <summary>
    /// Role-independent of the display text. UI styling and permission checks must use this,
    /// never <see cref="RoleName"/>, because the role name is translated for display.
    /// </summary>
    public bool IsAdmin { get; set; }
    public DateTime RegisteredOn { get; set; }
    public DateTime? LastUpdatedOn { get; set; }

    // sadhana summary
    public int CurrentStreak { get; set; }
    public int LongestStreak { get; set; }
    public int TotalMalas { get; set; }
    public int TotalDaysPracticed { get; set; }
    public DateOnly? LastPracticeDate { get; set; }

    /// <summary>The user's most recent sadhana sessions.</summary>
    public List<UserSadhanaEntryDto> RecentSadhana { get; set; } = new();
}

public class UserSadhanaEntryDto
{
    public DateOnly Date { get; set; }
    public string ChantName { get; set; } = string.Empty;
    public string? DeityName { get; set; }
    public string? CategoryName { get; set; }
    public int MalasCompleted { get; set; }
    public int TotalCount { get; set; }
}

/// <summary>The signed-in user's own profile, with a per-day sadhana timeline.</summary>
public class MyProfileDto
{
    public Guid UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? SeekerName { get; set; }
    public string Email { get; set; } = string.Empty;
    public string MobileNumber { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;

    /// <summary>
    /// Role-independent of the display text. UI styling and permission checks must use this,
    /// never <see cref="RoleName"/>, because the role name is translated for display.
    /// </summary>
    public bool IsAdmin { get; set; }
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

    /// <summary>Daily sadhana history, most recent day first.</summary>
    public List<SadhanaDayDto> Timeline { get; set; } = new();
}

/// <summary>Sets (or clears, when null) the signed-in user's preferred region.</summary>
public class UpdateDefaultRegionDto
{
    public Guid? RegionId { get; set; }
}

/// <summary>One day of the sadhana timeline: what was chanted and how much.</summary>
public class SadhanaDayDto
{
    public DateOnly Date { get; set; }
    public int TotalMalas { get; set; }
    public int TotalCount { get; set; }
    public List<UserSadhanaEntryDto> Entries { get; set; } = new();
}
