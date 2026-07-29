using Sanathana.Companion.Application.Common;
using Sanathana.Companion.Application.DTOs.Dashboard;
using Sanathana.Companion.Application.Interfaces;
using Sanathana.Companion.Domain.Entities;
using Sanathana.Companion.Domain.Interfaces;

namespace Sanathana.Companion.Application.Services;

public class DashboardService : IDashboardService
{
    private readonly IUnitOfWork _uow;

    public DashboardService(IUnitOfWork uow) => _uow = uow;

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5.5)); // IST

    public async Task<AdminDashboardDto> GetAdminStatsAsync(CancellationToken cancellationToken = default)
    {
        var nowUtc = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(nowUtc.AddHours(5.5)); // IST, matching the rest of the app
        var weekAgo = nowUtc.AddDays(-7);

        // Users table is small; one round trip covers total, role split and recent onboarding.
        var users = await _uow.Users.GetAllWithRolesAsync(cancellationToken);
        var totalUsers = users.Count;
        var admins = users.Count(u => string.Equals(u.Role?.RoleName, RoleNames.Admin, StringComparison.OrdinalIgnoreCase));
        var newThisWeek = users.Count(u => u.CreatedDate >= weekAgo);

        var totals = await _uow.Sadhana.GetTotalsAsync(today, cancellationToken);

        return new AdminDashboardDto
        {
            TotalUsers = totalUsers,
            TotalSeekers = totalUsers - admins,
            TotalAdmins = admins,
            NewThisWeek = newThisWeek,

            TotalMalas = totals.TotalMalas,
            TotalJapa = totals.TotalJapa,
            TotalSessions = totals.TotalSessions,
            ActiveToday = totals.ActiveToday,
            TotalDaysPracticed = totals.TotalDaysPracticed,
            LongestStreak = totals.LongestStreak,

            Deities = await _uow.Deities.CountAsync(cancellationToken),
            Chants = await _uow.Chants.CountAsync(cancellationToken),
            Festivals = await _uow.Festivals.CountAsync(cancellationToken),
            Regions = await _uow.Regions.CountAsync(cancellationToken),

            GeneratedAtUtc = nowUtc
        };
    }

    public async Task<TodayBhaktiDto> GetTodayBhaktiAsync(Guid? regionId = null, CancellationToken cancellationToken = default)
    {
        var today = Today;
        var weekday = today.DayOfWeek.ToString(); // "Sunday".. matches Deity.Days names

        var regionName = await ResolveRegionNameAsync(regionId, cancellationToken);

        var deities = (await _uow.Deities.ListWithoutImageAsync(cancellationToken))
            .Where(d => d.IsActive && RegionFilter.DeityInRegion(d, regionName)).ToList();
        var chantConfigs = (await _uow.ChantConfigs.GetFilteredAsync(null, null, null, cancellationToken))
            .Where(c => c.IsActive).ToList();
        var categoryNames = (await _uow.Chants.GetAllOrderedAsync(cancellationToken))
            .ToDictionary(c => c.Id, c => c.Name);

        // --- festival override: deities tied to a festival that falls today ---
        var todaysFestivals = (await _uow.Festivals.GetByYearAsync(today.Year, cancellationToken))
            .Where(f => f.IsActive && f.Date == today && RegionFilter.FestivalInRegion(f, regionId)).ToList();

        var matched = new List<(Deity Deity, string Reason)>();
        string? festivalName = null;

        if (todaysFestivals.Count > 0)
        {
            var festNames = todaysFestivals.Select(f => f.Name).ToList();
            foreach (var d in deities)
            {
                var hit = festNames.FirstOrDefault(n =>
                    SplitNames(d.Festivals).Contains(n, StringComparer.OrdinalIgnoreCase));
                if (hit is not null) matched.Add((d, $"Festival · {hit}"));
            }
            if (matched.Count > 0) festivalName = string.Join(", ", festNames);
        }

        bool isFestivalDay = matched.Count > 0;

        // --- day-based (no festival, or the festival mapped to no deity) ---
        if (matched.Count == 0)
        {
            foreach (var d in deities)
                if (SplitNames(d.Days).Contains(weekday, StringComparer.OrdinalIgnoreCase))
                    matched.Add((d, weekday));
        }

        var deityDtos = matched.Select(m => new TodayDeityDto
        {
            Id = m.Deity.Id,
            Name = m.Deity.Name,
            DeityType = m.Deity.DeityType,
            Description = m.Deity.Description,
            WelcomeNote = m.Deity.WelcomeNote,
            HasImage = m.Deity.ImageContentType != null,
            Days = SplitNames(m.Deity.Days),
            Reason = m.Reason,
            Sadhanas = chantConfigs
                .Where(c => CsvIds.Split(c.DeityIds).Contains(m.Deity.Id))
                .Select(c => new TodaySadhanaDto
                {
                    ChantConfigId = c.Id,
                    Name = c.Name,
                    CategoryName = c.Chant?.Name ?? categoryNames.GetValueOrDefault(c.ChantId, string.Empty),
                    HasAudio = c.AudioContentType != null
                }).ToList()
        }).ToList();

        return new TodayBhaktiDto
        {
            Date = today,
            DayOfWeek = weekday,
            IsFestivalDay = isFestivalDay,
            FestivalName = festivalName,
            Deities = deityDtos
        };
    }

    private static List<string> SplitNames(string? csv)
        => string.IsNullOrWhiteSpace(csv)
            ? new()
            : csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

    /// <summary>Deities are mapped to regions by NAME, so the chosen region id has to be resolved first.</summary>
    private async Task<string?> ResolveRegionNameAsync(Guid? regionId, CancellationToken cancellationToken)
    {
        if (regionId is null) return null;
        var region = await _uow.Regions.GetByIdAsync(regionId.Value, cancellationToken);
        return region?.Name;
    }

    public async Task<PrayersDto> GetPrayersAsync(Guid? regionId = null, CancellationToken cancellationToken = default)
    {
        var nowIst = TimeOnly.FromDateTime(DateTime.UtcNow.AddHours(5.5)); // IST

        var regionName = await ResolveRegionNameAsync(regionId, cancellationToken);
        var deitiesById = (await _uow.Deities.ListWithoutImageAsync(cancellationToken)).ToDictionary(d => d.Id, d => d);
        var deityNames = deitiesById.ToDictionary(kv => kv.Key, kv => kv.Value.Name);

        // Prayers are the chants with a configured time window (they can span any category).
        var configs = (await _uow.ChantConfigs.GetFilteredAsync(null, null, null, cancellationToken))
            .Where(c => c.IsActive && c.FromTime.HasValue && RegionFilter.ChantInRegion(c, regionName, deitiesById))
            .ToList();

        var categoryNames = (await _uow.Chants.GetAllOrderedAsync(cancellationToken))
            .ToDictionary(c => c.Id, c => c.Name);

        var prayers = configs
            .Select(c => new PrayerDto
            {
                ChantConfigId = c.Id,
                Name = c.Name,
                CategoryName = c.Chant?.Name ?? categoryNames.GetValueOrDefault(c.ChantId, string.Empty),
                DeityNames = CsvIds.Split(c.DeityIds).Where(deityNames.ContainsKey).Select(id => deityNames[id]).ToList(),
                FromTime = c.FromTime,
                ToTime = c.ToTime,
                TimeDescription = c.TimeDescription,
                Slot = ClassifySlot(c.TimeDescription, c.FromTime),
                IsActiveNow = IsActiveNow(c.FromTime, c.ToTime, nowIst)
            })
            .OrderByDescending(p => p.IsActiveNow)
            .ThenBy(p => p.FromTime)
            .ThenBy(p => p.Name)
            .ToList();

        return new PrayersDto { CurrentTime = nowIst, Prayers = prayers };
    }

    /// <summary>Buckets a prayer into a time-of-day slot from its description, falling back to its start hour.</summary>
    private static string ClassifySlot(string? timeDescription, TimeOnly? from)
    {
        var td = timeDescription?.ToLowerInvariant() ?? string.Empty;
        if (td.Contains("food") || td.Contains("meal") || td.Contains("bhojan")) return "Food";
        if (td.Contains("morning") || td.Contains("pratah") || td.Contains("dawn") || td.Contains("wake")) return "Morning";
        if (td.Contains("afternoon") || td.Contains("noon") || td.Contains("midday")) return "Afternoon";
        if (td.Contains("evening") || td.Contains("sandhya") || td.Contains("dusk")) return "Evening";
        if (td.Contains("night") || td.Contains("ratri") || td.Contains("bed") || td.Contains("sleep")) return "Night";

        if (from is { } f)
        {
            return f.Hour switch
            {
                >= 4 and < 11 => "Morning",
                >= 11 and < 16 => "Afternoon",
                >= 16 and < 20 => "Evening",
                _ => "Night"
            };
        }

        return "Anytime";
    }

    /// <summary>Whether <paramref name="now"/> falls in [from, to], handling windows that wrap past midnight.</summary>
    private static bool IsActiveNow(TimeOnly? from, TimeOnly? to, TimeOnly now)
    {
        if (from is not { } f || to is not { } t) return false;
        return f <= t ? now >= f && now <= t : now >= f || now <= t;
    }
}
