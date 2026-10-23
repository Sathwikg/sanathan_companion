namespace App.Core.Models;

public class UserListItem
{
    public Guid UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? SeekerName { get; set; }
    public string Email { get; set; } = string.Empty;
    public string MobileNumber { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;

    /// <summary>Independent of the display text — use this for styling and permission checks,
    /// never <see cref="RoleName"/>, which is translated.</summary>
    public bool IsAdmin { get; set; }
    public DateTime RegisteredOn { get; set; }

    /// <summary>False once an administrator has closed the account.</summary>
    public bool IsActive { get; set; }

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

    /// <summary>Independent of the display text — use this for styling and permission checks,
    /// never <see cref="RoleName"/>, which is translated.</summary>
    public bool IsAdmin { get; set; }
    public DateTime RegisteredOn { get; set; }
    public DateTime? LastUpdatedOn { get; set; }

    /// <summary>False once an administrator has closed the account.</summary>
    public bool IsActive { get; set; }
    public string? DefaultRegionName { get; set; }
    public bool GoogleLinked { get; set; }

    public int CurrentStreak { get; set; }
    public int LongestStreak { get; set; }
    public int TotalMalas { get; set; }
    public int TotalDaysPracticed { get; set; }
    public DateOnly? LastPracticeDate { get; set; }

    /// <summary>Whole days since <see cref="LastPracticeDate"/>, counted in IST; null if never.</summary>
    public int? DaysSinceLastPractice { get; set; }

    public UserSadhanaInsights Insights { get; set; } = new();
}

/// <summary>The per-seeker dashboard. Every date is an IST calendar day.</summary>
public class UserSadhanaInsights
{
    /// <summary>Today in IST. The calendar ends here, whatever the device's time zone.</summary>
    public DateOnly Today { get; set; }

    public long TotalJapa { get; set; }
    public int ChantsPracticed { get; set; }
    public DateOnly? FirstPracticeDate { get; set; }
    public double AverageMalasPerDay { get; set; }

    /// <summary>Days with a mala in the last <see cref="WindowDays"/> days (30, or fewer for a new account).</summary>
    public int ActiveDays { get; set; }
    public int WindowDays { get; set; }

    public int MalasLast30Days { get; set; }
    public int MalasPrevious30Days { get; set; }

    public DateOnly? BestDayDate { get; set; }
    public int BestDayMalas { get; set; }

    /// <summary>The Monday the calendar starts on, <see cref="ActivityWeeks"/> columns from here.</summary>
    public DateOnly ActivityFrom { get; set; }
    public int ActivityWeeks { get; set; }

    /// <summary>Only the days with practice.</summary>
    public List<UserActivityDay> Activity { get; set; } = new();

    /// <summary>Monday first, all seven days present.</summary>
    public List<UserWeekdayStat> Weekdays { get; set; } = new();

    public List<UserChantStat> TopChants { get; set; } = new();
    public List<UserDeityStat> TopDeities { get; set; } = new();
}

public class UserActivityDay
{
    public DateOnly Date { get; set; }
    public int Malas { get; set; }
    public long Japa { get; set; }
    public int Chants { get; set; }
}

public class UserWeekdayStat
{
    /// <summary>0 = Monday … 6 = Sunday.</summary>
    public int Day { get; set; }
    public int Malas { get; set; }
    public int Days { get; set; }
}

public class UserChantStat
{
    public string ChantName { get; set; } = string.Empty;
    public string? DeityName { get; set; }
    public int Malas { get; set; }
    public long Japa { get; set; }
    public int Days { get; set; }
    public DateOnly LastDate { get; set; }
}

public class UserDeityStat
{
    public string DeityName { get; set; } = string.Empty;
    public int Malas { get; set; }
    public long Japa { get; set; }
}

/// <summary>One page of a seeker's history: <see cref="PageSize"/> practice days, newest first.</summary>
public class UserSadhanaPage
{
    public List<SadhanaDay> Items { get; set; } = new();

    /// <summary>Days with practice, across every page.</summary>
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
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

    /// <summary>What the role means, as the Role master describes it. Shown by the "Your Role" popup.</summary>
    public string? RoleDescription { get; set; }

    /// <summary>Independent of the display text — use this for styling and permission checks,
    /// never <see cref="RoleName"/>, which is translated.</summary>
    public bool IsAdmin { get; set; }
    public DateTime RegisteredOn { get; set; }
    public DateTime? LastUpdatedOn { get; set; }

    /// <summary>The user's preferred region; seeds the app's region selector.</summary>
    public Guid? DefaultRegionId { get; set; }
    public string? DefaultRegionName { get; set; }

    /// <summary>True once a Google account has been connected to this account.</summary>
    public bool GoogleLinked { get; set; }

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

/// <summary>Confirms deletion of the caller's own account.</summary>
public class DeleteAccountRequest
{
    public string Password { get; set; } = string.Empty;
}

/// <summary>Changes the caller's own password.</summary>
public class ChangePasswordRequest
{
    public string CurrentPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
    public string ConfirmNewPassword { get; set; } = string.Empty;
}

/// <summary>Whether an account may be signed in to.</summary>
public class SetUserStatusRequest
{
    public bool IsActive { get; set; }
}
