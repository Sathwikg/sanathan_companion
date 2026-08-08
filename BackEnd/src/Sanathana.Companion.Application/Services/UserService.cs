using Sanathana.Companion.Application.Common;
using Sanathana.Companion.Application.DTOs.Users;
using Sanathana.Companion.Application.Interfaces;
using Sanathana.Companion.Domain.Exceptions;
using Sanathana.Companion.Domain.Interfaces;

namespace Sanathana.Companion.Application.Services;

public class UserService : IUserService
{
    private readonly IUnitOfWork _uow;

    public UserService(IUnitOfWork uow) => _uow = uow;

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
                CurrentStreak = DisplayStreak(streak),
                TotalMalas = streak?.TotalMalas ?? 0
            });
        }

        return result;
    }

    public async Task<UserProfileDto?> GetProfileAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var u = await _uow.Users.GetWithRoleAsync(userId, cancellationToken);
        if (u is null) return null;

        var streak = await _uow.Sadhana.GetStreakAsync(userId, cancellationToken);

        var from = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5.5)).AddDays(-30);
        var to = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5.5));
        var history = await _uow.Sadhana.GetHistoryAsync(userId, from, to, cancellationToken);

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
            CurrentStreak = DisplayStreak(streak),
            LongestStreak = streak?.LongestStreak ?? 0,
            TotalMalas = streak?.TotalMalas ?? 0,
            TotalDaysPracticed = streak?.TotalDaysPracticed ?? 0,
            LastPracticeDate = streak?.LastPracticeDate,
            RecentSadhana = history
                .OrderByDescending(h => h.Date).Take(15)
                .Select(h => new UserSadhanaEntryDto
                {
                    Date = h.Date,
                    ChantName = h.ChantName,
                    DeityName = h.DeityName,
                    CategoryName = h.CategoryName,
                    MalasCompleted = h.MalasCompleted,
                    TotalCount = h.TotalCount
                }).ToList()
        };
    }

    public async Task<MyProfileDto?> GetMyProfileAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var u = await _uow.Users.GetWithRoleAsync(userId, cancellationToken);
        if (u is null) return null;

        var streak = await _uow.Sadhana.GetStreakAsync(userId, cancellationToken);

        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5.5)); // IST
        var from = today.AddDays(-90);
        var history = await _uow.Sadhana.GetHistoryAsync(userId, from, today, cancellationToken);

        var timeline = history
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
            IsAdmin = string.Equals(u.Role?.RoleName, RoleNames.Admin, StringComparison.OrdinalIgnoreCase),
            RegisteredOn = u.CreatedDate,
            LastUpdatedOn = u.ModifiedDate,
            DefaultRegionId = u.DefaultRegionId,
            DefaultRegionName = regionName,
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

    /// <summary>A streak counts only if the last practice was today or yesterday, else it has lapsed.</summary>
    private static int DisplayStreak(Domain.Entities.SadhanaStreak? s)
    {
        if (s is null) return 0;
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5.5));
        bool alive = s.LastPracticeDate == today || s.LastPracticeDate == today.AddDays(-1);
        return alive ? s.CurrentStreak : 0;
    }
}
