using Sanathana.Companion.Application.Common;
using Sanathana.Companion.Domain.Common;
using Sanathana.Companion.Application.DTOs.Users;
using Sanathana.Companion.Application.Interfaces;
using Sanathana.Companion.Domain.Exceptions;
using Sanathana.Companion.Domain.Interfaces;

namespace Sanathana.Companion.Application.Services;

public class UserService : IUserService
{
    private readonly IUnitOfWork _uow;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IAccountDataReader _accountData;

    public UserService(IUnitOfWork uow, IPasswordHasher passwordHasher, IAccountDataReader accountData)
    {
        _uow = uow;
        _passwordHasher = passwordHasher;
        _accountData = accountData;
    }

    public async Task<IReadOnlyList<UserListItemDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var users = await _uow.Users.GetAllWithRolesAsync(cancellationToken);

        // Load all streaks in one query, then index — avoids a per-user round trip (N+1).
        var streaks = (await _uow.Sadhana.GetStreaksAsync(users.Select(u => u.UserId), cancellationToken))
            .ToDictionary(s => s.UserId, s => s);

        var result = new List<UserListItemDto>(users.Count);
        foreach (var u in users)
        {
            streaks.TryGetValue(u.UserId, out var streak);
            result.Add(new UserListItemDto
            {
                UserId = u.UserId,
                FullName = u.FullName,
                SeekerName = u.SeekerName,
                Email = u.Email,
                MobileNumber = u.MobileNumber,
                RoleName = u.Role?.RoleName ?? string.Empty,
                IsAdmin = string.Equals(u.Role?.RoleName, RoleNames.Admin, StringComparison.OrdinalIgnoreCase),
                RegisteredOn = u.CreatedDate,
                IsActive = u.IsActive,
                CurrentStreak = DisplayStreak(streak),
                TotalMalas = streak?.TotalMalas ?? 0
            });
        }

        return result;
    }

    /// <summary>Weeks in the admin profile's practice calendar: about six months.</summary>
    private const int ActivityWeeks = 26;

    /// <summary>Rows in the profile's top chants and top deities.</summary>
    private const int TopListSize = 5;

    /// <summary>A month of days is the most one timeline page may ask for.</summary>
    private const int MaxTimelinePageSize = 31;

    public async Task<UserProfileDto?> GetProfileAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var u = await _uow.Users.GetWithRoleAsync(userId, cancellationToken);
        if (u is null) return null;

        var streak = await _uow.Sadhana.GetStreakAsync(userId, cancellationToken);
        var days = await _uow.Sadhana.GetDailyTotalsAsync(userId, cancellationToken);
        var chants = await _uow.Sadhana.GetChantTotalsAsync(userId, cancellationToken);

        string? regionName = null;
        if (u.DefaultRegionId is { } rid)
            regionName = (await _uow.Regions.GetByIdAsync(rid, cancellationToken))?.Name;

        var today = TodayIst();

        return new UserProfileDto
        {
            UserId = u.UserId,
            FullName = u.FullName,
            SeekerName = u.SeekerName,
            Email = u.Email,
            MobileNumber = u.MobileNumber,
            RoleName = u.Role?.RoleName ?? string.Empty,
            IsAdmin = string.Equals(u.Role?.RoleName, RoleNames.Admin, StringComparison.OrdinalIgnoreCase),
            RegisteredOn = u.CreatedDate,
            LastUpdatedOn = u.ModifiedDate,
            IsActive = u.IsActive,
            DefaultRegionName = regionName,
            GoogleLinked = u.GoogleSubject is not null,
            CurrentStreak = DisplayStreak(streak),
            LongestStreak = streak?.LongestStreak ?? 0,
            TotalMalas = streak?.TotalMalas ?? 0,
            TotalDaysPracticed = streak?.TotalDaysPracticed ?? 0,
            LastPracticeDate = streak?.LastPracticeDate,
            DaysSinceLastPractice = streak?.LastPracticeDate is { } last ? today.DayNumber - last.DayNumber : null,
            Insights = BuildInsights(days, chants, streak, today, DateOnly.FromDateTime(u.CreatedDate.AddHours(5.5)))
        };
    }

    public async Task<UserSadhanaPageDto?> GetSadhanaPageAsync(Guid userId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        if (!await _uow.Users.AnyAsync(u => u.UserId == userId, cancellationToken)) return null;

        pageSize = Math.Clamp(pageSize, 1, MaxTimelinePageSize);

        // Paged over days rather than log rows, so one busy day is never split across two pages.
        var days = await _uow.Sadhana.GetDailyTotalsAsync(userId, cancellationToken);
        var pageCount = Math.Max(1, (days.Count + pageSize - 1) / pageSize);
        page = Math.Clamp(page, 1, pageCount);

        var slice = days.OrderByDescending(d => d.Date)
            .Skip((page - 1) * pageSize).Take(pageSize).ToList();

        var items = new List<SadhanaDayDto>();
        if (slice.Count > 0)
        {
            // The page's days are consecutive among the days practised, so the range from its
            // oldest to its newest holds those days and no others.
            var logs = await _uow.Sadhana.GetHistoryAsync(userId, slice[^1].Date, slice[0].Date, cancellationToken);
            items = ToTimeline(logs.Where(l => l.TotalCount > 0));
        }

        return new UserSadhanaPageDto { Items = items, TotalCount = days.Count, Page = page, PageSize = pageSize };
    }

    private static UserSadhanaInsightsDto BuildInsights(
        IReadOnlyList<SadhanaDayTotal> days,
        IReadOnlyList<SadhanaChantTotal> chants,
        Domain.Entities.SadhanaStreak? streak,
        DateOnly today,
        DateOnly registered)
    {
        // A day counts as practised once a mala is complete, the same rule the streak uses.
        var practised = days.Where(d => d.Malas > 0).ToList();

        // The calendar's columns are Monday-to-Sunday weeks, the last one holding today.
        var thisMonday = today.AddDays(-MondayIndex(today));
        var activityFrom = thisMonday.AddDays(-7 * (ActivityWeeks - 1));

        var window = Math.Clamp(today.DayNumber - registered.DayNumber + 1, 1, 30);
        var windowFrom = today.AddDays(1 - window);
        var last30From = today.AddDays(-29);
        var previous30From = today.AddDays(-59);

        var best = practised.OrderByDescending(d => d.Malas).ThenByDescending(d => d.Date).FirstOrDefault();

        var weekdays = Enumerable.Range(0, 7).Select(i => new UserWeekdayStatDto { Day = i }).ToList();
        foreach (var d in practised)
        {
            var w = weekdays[MondayIndex(d.Date)];
            w.Malas += d.Malas;
            w.Days++;
        }

        return new UserSadhanaInsightsDto
        {
            Today = today,
            TotalJapa = days.Sum(d => d.Japa),
            ChantsPracticed = chants.Count,
            FirstPracticeDate = days.Count > 0 ? days[0].Date : null,
            AverageMalasPerDay = streak is { TotalDaysPracticed: > 0 }
                ? Math.Round(streak.TotalMalas / (double)streak.TotalDaysPracticed, 1)
                : 0,
            ActiveDays = practised.Count(d => d.Date >= windowFrom && d.Date <= today),
            WindowDays = window,
            MalasLast30Days = days.Where(d => d.Date >= last30From && d.Date <= today).Sum(d => d.Malas),
            MalasPrevious30Days = days.Where(d => d.Date >= previous30From && d.Date < last30From).Sum(d => d.Malas),
            BestDayDate = best?.Date,
            BestDayMalas = best?.Malas ?? 0,
            ActivityFrom = activityFrom,
            ActivityWeeks = ActivityWeeks,
            Activity = days
                .Where(d => d.Date >= activityFrom && d.Date <= today)
                .Select(d => new UserActivityDayDto { Date = d.Date, Malas = d.Malas, Japa = d.Japa, Chants = d.Chants })
                .ToList(),
            Weekdays = weekdays,
            TopChants = chants
                .OrderByDescending(c => c.Malas).ThenByDescending(c => c.Japa).ThenBy(c => c.ChantName)
                .Take(TopListSize)
                .Select(c => new UserChantStatDto
                {
                    ChantName = c.ChantName,
                    DeityName = c.DeityName,
                    Malas = c.Malas,
                    Japa = c.Japa,
                    Days = c.Days,
                    LastDate = c.LastDate
                }).ToList(),
            TopDeities = chants
                .Where(c => !string.IsNullOrWhiteSpace(c.DeityName))
                .GroupBy(c => c.DeityName!)
                .Select(g => new UserDeityStatDto { DeityName = g.Key, Malas = g.Sum(c => c.Malas), Japa = g.Sum(c => c.Japa) })
                .OrderByDescending(d => d.Malas).ThenByDescending(d => d.Japa)
                .Take(TopListSize)
                .ToList()
        };
    }

    /// <summary>0 for Monday through 6 for Sunday. DayOfWeek starts the week on Sunday.</summary>
    private static int MondayIndex(DateOnly date) => ((int)date.DayOfWeek + 6) % 7;

    public async Task<MyProfileDto?> GetMyProfileAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var u = await _uow.Users.GetWithRoleAsync(userId, cancellationToken);
        if (u is null) return null;

        var streak = await _uow.Sadhana.GetStreakAsync(userId, cancellationToken);

        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5.5)); // IST
        var from = today.AddDays(-90);
        var history = await _uow.Sadhana.GetHistoryAsync(userId, from, today, cancellationToken);

        var timeline = ToTimeline(history);

        string? regionName = null;
        if (u.DefaultRegionId is { } rid)
            regionName = (await _uow.Regions.GetByIdAsync(rid, cancellationToken))?.Name;

        return new MyProfileDto
        {
            UserId = u.UserId,
            FullName = u.FullName,
            SeekerName = u.SeekerName,
            Email = u.Email,
            MobileNumber = u.MobileNumber,
            RoleName = u.Role?.RoleName ?? string.Empty,
            RoleDescription = u.Role?.Description,
            IsAdmin = string.Equals(u.Role?.RoleName, RoleNames.Admin, StringComparison.OrdinalIgnoreCase),
            RegisteredOn = u.CreatedDate,
            LastUpdatedOn = u.ModifiedDate,
            DefaultRegionId = u.DefaultRegionId,
            DefaultRegionName = regionName,
            GoogleLinked = u.GoogleSubject is not null,
            CurrentStreak = DisplayStreak(streak),
            LongestStreak = streak?.LongestStreak ?? 0,
            TotalMalas = streak?.TotalMalas ?? 0,
            TotalDaysPracticed = streak?.TotalDaysPracticed ?? 0,
            LastPracticeDate = streak?.LastPracticeDate,
            PracticedToday = streak?.LastPracticeDate == today,
            Timeline = timeline
        };
    }

    public async Task UpdateDefaultRegionAsync(Guid userId, Guid? regionId, CancellationToken cancellationToken = default)
    {
        // Tracked load (no navigation) — Update() on a no-tracking graph would also mark Role modified.
        var user = await _uow.Users.GetByIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException("Your account was not found.");

        if (regionId is { } rid)
        {
            var region = await _uow.Regions.GetByIdAsync(rid, cancellationToken);
            if (region is null || !region.IsActive)
                throw new BadRequestException("Please choose a valid region.");
        }
        else
        {
            var role = await _uow.Roles.GetByIdAsync(user.RoleId, cancellationToken);
            // Only administrators may view every region; seekers always browse one region.
            if (!string.Equals(role?.RoleName, RoleNames.Admin, StringComparison.OrdinalIgnoreCase))
                throw new BadRequestException("Please choose a region.");
        }

        user.DefaultRegionId = regionId;
        // Deliberately NOT calling Update(): the entity is tracked, so EF writes only the changed
        // column. Update() marks every property modified, which would needlessly rewrite the
        // password hash on a preference change.
        await _uow.SaveChangesAsync(cancellationToken);
    }

    public async Task SetActiveAsync(Guid userId, bool isActive, Guid actingUserId, CancellationToken cancellationToken = default)
    {
        // An administrator locking themselves out is a support call, not a security feature.
        if (userId == actingUserId)
            throw new BadRequestException("You cannot close your own account from here.");

        // The seeded administrator is the account the bootstrapper re-opens from
        // Admin__InitialPassword, and that path only ever touches the password hash. Closing it
        // would leave no way back in.
        if (userId == WellKnownIds.AdminUser)
            throw new BadRequestException("The built-in administrator account cannot be closed.");

        var user = await _uow.Users.GetByIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException("That account was not found.");

        user.IsActive = isActive;

        if (!isActive)
        {
            // Closing an account has to end the sessions it already holds, or it means nothing for
            // the next two hours. TokensValidFromUtc kills the access tokens; the revoke kills the
            // refresh tokens that would otherwise mint new ones.
            var now = DateTime.UtcNow;
            user.TokensValidFromUtc = now;
            await _uow.RefreshTokens.RevokeAllForUserAsync(userId, "disabled", now, cancellationToken);
        }

        // Tracked, so EF writes only the changed columns; see UpdateDefaultRegionAsync.
        await _uow.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Logs grouped into days, newest first; within a day, most malas first.</summary>
    private static List<SadhanaDayDto> ToTimeline(IEnumerable<Domain.Entities.SadhanaLog> logs) => logs
        .GroupBy(h => h.Date)
        .OrderByDescending(g => g.Key)
        .Select(g => new SadhanaDayDto
        {
            Date = g.Key,
            TotalMalas = g.Sum(x => x.MalasCompleted),
            TotalCount = g.Sum(x => x.TotalCount),
            Entries = g
                .OrderByDescending(x => x.MalasCompleted)
                .ThenBy(x => x.ChantName)
                .Select(x => new UserSadhanaEntryDto
                {
                    Date = x.Date,
                    ChantName = x.ChantName,
                    DeityName = x.DeityName,
                    CategoryName = x.CategoryName,
                    MalasCompleted = x.MalasCompleted,
                    TotalCount = x.TotalCount
                }).ToList()
        }).ToList();

    private static DateOnly TodayIst() => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5.5));

    /// <summary>A streak counts only if the last practice was today or yesterday, else it has lapsed.</summary>
    private static int DisplayStreak(Domain.Entities.SadhanaStreak? s)
    {
        if (s is null) return 0;
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5.5));
        bool alive = s.LastPracticeDate == today || s.LastPracticeDate == today.AddDays(-1);
        return alive ? s.CurrentStreak : 0;
    }

    public Task<MyDataExportDto?> ExportMyDataAsync(Guid userId, CancellationToken cancellationToken = default)
        => _accountData.ExportAsync(userId, cancellationToken);

    public async Task<bool> DeleteMyAccountAsync(Guid userId, string password, CancellationToken cancellationToken = default)
    {
        var user = await _uow.Users.GetByIdAsync(userId, cancellationToken);
        if (user is null) return false;

        // Irreversible, so it is not enough to hold the session — the password is re-checked. A
        // borrowed or stolen phone must not be able to erase somebody's practice history.
        if (!_passwordHasher.Verify(password, user.PasswordHash)) return false;

        await _uow.Users.PurgeUserDataAsync(userId, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);

        return true;
    }
}
