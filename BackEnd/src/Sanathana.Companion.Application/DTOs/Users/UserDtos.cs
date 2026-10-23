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

    /// <summary>False once an administrator has closed the account.</summary>
    public bool IsActive { get; set; }

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

    /// <summary>False once an administrator has closed the account.</summary>
    public bool IsActive { get; set; }
    public string? DefaultRegionName { get; set; }
    public bool GoogleLinked { get; set; }

    // sadhana summary
    public int CurrentStreak { get; set; }
    public int LongestStreak { get; set; }
    public int TotalMalas { get; set; }
    public int TotalDaysPracticed { get; set; }
    public DateOnly? LastPracticeDate { get; set; }

    /// <summary>Whole days since <see cref="LastPracticeDate"/>, counted in IST; null if never.</summary>
    public int? DaysSinceLastPractice { get; set; }

    /// <summary>The per-seeker dashboard. The day-by-day history is paged separately.</summary>
    public UserSadhanaInsightsDto Insights { get; set; } = new();
}

/// <summary>
/// What an administrator needs to judge one seeker's practice at a glance. Every figure is
/// computed in IST, the calendar the streak itself is kept in.
/// </summary>
public class UserSadhanaInsightsDto
{
    /// <summary>Today in IST. The calendar ends here, whatever time zone the admin is in.</summary>
    public DateOnly Today { get; set; }

    public long TotalJapa { get; set; }
    public int ChantsPracticed { get; set; }
    public DateOnly? FirstPracticeDate { get; set; }

    /// <summary>Lifetime malas per day practised, to one decimal place.</summary>
    public double AverageMalasPerDay { get; set; }

    /// <summary>Days with at least one mala in the last <see cref="WindowDays"/> days.</summary>
    public int ActiveDays { get; set; }

    /// <summary>30, or fewer for an account younger than that, so a new seeker is not marked down.</summary>
    public int WindowDays { get; set; }

    public int MalasLast30Days { get; set; }
    public int MalasPrevious30Days { get; set; }

    public DateOnly? BestDayDate { get; set; }
    public int BestDayMalas { get; set; }

    /// <summary>The Monday the practice calendar starts on; <see cref="ActivityWeeks"/> columns from here.</summary>
    public DateOnly ActivityFrom { get; set; }
    public int ActivityWeeks { get; set; }

    /// <summary>Only the days with practice; the client fills the gaps.</summary>
    public List<UserActivityDayDto> Activity { get; set; } = new();

    /// <summary>Monday first, all seven days present.</summary>
    public List<UserWeekdayStatDto> Weekdays { get; set; } = new();

    public List<UserChantStatDto> TopChants { get; set; } = new();
    public List<UserDeityStatDto> TopDeities { get; set; } = new();
}

public class UserActivityDayDto
{
    public DateOnly Date { get; set; }
    public int Malas { get; set; }
    public long Japa { get; set; }
    public int Chants { get; set; }
}

public class UserWeekdayStatDto
{
    /// <summary>0 = Monday … 6 = Sunday.</summary>
    public int Day { get; set; }
    public int Malas { get; set; }
    public int Days { get; set; }
}

public class UserChantStatDto
{
    public string ChantName { get; set; } = string.Empty;
    public string? DeityName { get; set; }
    public int Malas { get; set; }
    public long Japa { get; set; }
    public int Days { get; set; }
    public DateOnly LastDate { get; set; }
}

public class UserDeityStatDto
{
    public string DeityName { get; set; } = string.Empty;
    public int Malas { get; set; }
    public long Japa { get; set; }
}

/// <summary>One page of a seeker's history, a page being a run of practice days, newest first.</summary>
public class UserSadhanaPageDto
{
    public List<SadhanaDayDto> Items { get; set; } = new();

    /// <summary>Days with practice, across every page.</summary>
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
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

    /// <summary>What the role means, as the Role master describes it. Shown by the "Your Role" popup.</summary>
    public string? RoleDescription { get; set; }

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

    /// <summary>True once a Google account has been connected, so the profile can say so.</summary>
    public bool GoogleLinked { get; set; }

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
