using Sanathana.Companion.Application.Common;
using Sanathana.Companion.Application.DTOs.Sadhana;
using Sanathana.Companion.Application.Interfaces;
using Sanathana.Companion.Domain.Entities;
using Sanathana.Companion.Domain.Exceptions;
using Sanathana.Companion.Domain.Interfaces;

namespace Sanathana.Companion.Application.Services;

public class SadhanaService : ISadhanaService
{
    /// <summary>Default mala size when a chant's category has no configured count.</summary>
    private const int DefaultMala = 108;

    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public SadhanaService(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    private Guid UserId => _currentUser.UserId ?? throw new BadRequestException("Could not identify the current user.");

    /// <summary>
    /// The two lists a guest may browse ask for the caller's own progress as an extra. A guest has
    /// none, so they get the same chants with nothing logged rather than an error.
    /// </summary>
    private async Task<IReadOnlyList<SadhanaLog>> TodayLogsOrNoneAsync(DateOnly today, CancellationToken cancellationToken)
        => _currentUser.UserId is { } userId
            ? await _uow.Sadhana.GetLogsForDateAsync(userId, today, cancellationToken)
            : Array.Empty<SadhanaLog>();
    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5.5));   // IST

    public async Task<SadhanaTodayDto> GetTodayAsync(Guid? regionId, CancellationToken cancellationToken = default)
    {
        var today = Today;

        var regionName = await ResolveRegionNameAsync(regionId, cancellationToken);

        var deities = await _uow.Deities.ListWithoutImageAsync(cancellationToken);
        // Only deities of the chosen region drive today's recommendations.
        var deityById = deities.Where(d => d.IsActive && RegionFilter.DeityInRegion(d, regionName))
            .ToDictionary(d => d.Id, d => d);
        var deityNames = deities.ToDictionary(d => d.Id, d => d.Name);
        var chants = (await _uow.ChantConfigs.GetFilteredAsync(null, null, null, cancellationToken))
            .Where(c => c.IsActive).ToList();
        var categories = (await _uow.Chants.GetAllOrderedAsync(cancellationToken)).ToDictionary(c => c.Id, c => c);

        var todayLogs = (await TodayLogsOrNoneAsync(today, cancellationToken)).ToList();
        var progressByChant = todayLogs.ToDictionary(l => l.ChantConfigId, l => l);

        var weekday = today.DayOfWeek.ToString();   // "Sunday" .. matches Deity.Days names

        // --- festival override ---
        string? festivalName = null;
        var festivals = await _uow.Festivals.GetByYearAsync(today.Year, cancellationToken);
        var todaysFestivals = festivals
            .Where(f => f.IsActive && f.Date == today && RegionFilter.FestivalInRegion(f, regionId))
            .ToList();

        var recommendations = new List<SadhanaChantDto>();

        if (todaysFestivals.Count > 0)
        {
            festivalName = string.Join(", ", todaysFestivals.Select(f => f.Name));
            foreach (var fest in todaysFestivals)
            {
                var festDeities = deityById.Values
                    .Where(d => SplitNames(d.Festivals).Contains(fest.Name, StringComparer.OrdinalIgnoreCase))
                    .ToList();
                AddRecommendations(recommendations, festDeities, chants, categories, deityNames, progressByChant, $"Festival · {fest.Name}");
            }
        }

        bool festivalDrivenRecommendations = recommendations.Count > 0;

        // --- day-based (used when no festival, or as fallback when the festival maps to no chants) ---
        if (recommendations.Count == 0)
        {
            var dayDeities = deityById.Values
                .Where(d => SplitNames(d.Days).Contains(weekday, StringComparer.OrdinalIgnoreCase))
                .ToList();
            AddRecommendations(recommendations, dayDeities, chants, categories, deityNames, progressByChant, weekday);
        }

        var streak = _currentUser.UserId is { } userId
            ? await _uow.Sadhana.GetStreakAsync(userId, cancellationToken)
            : null;

        await ApplyOwnMalaSizesAsync(recommendations, cancellationToken);

        return new SadhanaTodayDto
        {
            Date = today,
            DayOfWeek = weekday,
            IsFestivalDay = todaysFestivals.Count > 0 && festivalDrivenRecommendations,
            FestivalName = festivalName,
            Recommendations = recommendations,
            TodaySessions = todayLogs.Select(ToSession).ToList(),
            Streak = ToStreakDto(streak, today),
            TodayMalas = todayLogs.Sum(l => l.MalasCompleted),
            TodayChantsPracticed = todayLogs.Count(l => l.TotalCount > 0)
        };
    }

    public async Task<IReadOnlyList<SadhanaChantDto>> GetChantsAsync(string? search, Guid? regionId = null, CancellationToken cancellationToken = default)
    {
        var today = Today;

        var regionName = await ResolveRegionNameAsync(regionId, cancellationToken);
        var deities = await _uow.Deities.ListWithoutImageAsync(cancellationToken);
        var deitiesById = deities.ToDictionary(d => d.Id, d => d);

        var categories = (await _uow.Chants.GetAllOrderedAsync(cancellationToken)).ToDictionary(c => c.Id, c => c);
        var deityNames = deities.ToDictionary(d => d.Id, d => d.Name);

        // Filtered here rather than in SQL: the search box promises "name, deity or category", and
        // a chant's deities are a CSV of ids that only resolve to names in memory. The active list
        // is a few dozen rows.
        var term = search?.Trim();
        var chants = (await _uow.ChantConfigs.GetFilteredAsync(null, null, null, cancellationToken))
            .Where(c => c.IsActive && RegionFilter.ChantInRegion(c, regionName, deitiesById))
            .Where(c => string.IsNullOrEmpty(term) || MatchesSearch(c, term, deityNames))
            .ToList();
        var progressByChant = (await TodayLogsOrNoneAsync(today, cancellationToken))
            .ToDictionary(l => l.ChantConfigId, l => l);

        var list = chants.Select(c => ToChantDto(c, categories, deityNames, progressByChant, null)).ToList();
        await ApplyOwnMalaSizesAsync(list, cancellationToken);
        return list;
    }

    public async Task<SadhanaChantDetailDto?> GetChantAsync(Guid chantConfigId, CancellationToken cancellationToken = default)
    {
        var userId = UserId;
        var today = Today;

        var c = await _uow.ChantConfigs.GetDetailAsync(chantConfigId, cancellationToken);
        if (c is null) return null;

        var categories = (await _uow.Chants.GetAllOrderedAsync(cancellationToken)).ToDictionary(x => x.Id, x => x);
        var deities = await _uow.Deities.ListWithoutImageAsync(cancellationToken);
        var deityNames = deities.ToDictionary(d => d.Id, d => d.Name);
        var log = await _uow.Sadhana.GetLogAsync(userId, today, chantConfigId, cancellationToken);
        var setting = await _uow.Sadhana.GetChantSettingAsync(userId, chantConfigId, cancellationToken);

        return ToDetailDto(c, categories, deityNames, log, setting);
    }

    public async Task<SadhanaChantDetailDto> SaveChantSettingsAsync(Guid chantConfigId, SaveChantSettingsDto dto, CancellationToken cancellationToken = default)
    {
        var userId = UserId;

        if (dto.MalaSize is { } size && (size < UserChantSetting.MinMalaSize || size > UserChantSetting.MaxMalaSize))
            throw new BadRequestException(
                $"A mala must be between {UserChantSetting.MinMalaSize} and {UserChantSetting.MaxMalaSize:N0} chants.");

        var c = await _uow.ChantConfigs.GetDetailAsync(chantConfigId, cancellationToken)
            ?? throw new NotFoundException($"Chant '{chantConfigId}' was not found.");

        var setting = await _uow.Sadhana.GetChantSettingAsync(userId, chantConfigId, cancellationToken);
        if (setting is null)
        {
            setting = new UserChantSetting { Id = Guid.NewGuid(), UserId = userId, ChantConfigId = chantConfigId };
            await _uow.Sadhana.AddChantSettingAsync(setting, cancellationToken);
        }

        setting.MalaSize = dto.MalaSize;
        setting.VibrationEnabled = dto.VibrationEnabled;
        await _uow.SaveChangesAsync(cancellationToken);

        // Nothing about today's log changes: completed malas stay banked, and the mala in progress
        // keeps its count and is simply measured against the new size from the next chant on.
        var categories = (await _uow.Chants.GetAllOrderedAsync(cancellationToken)).ToDictionary(x => x.Id, x => x);
        var deityNames = (await _uow.Deities.ListWithoutImageAsync(cancellationToken)).ToDictionary(d => d.Id, d => d.Name);
        var log = await _uow.Sadhana.GetLogAsync(userId, Today, chantConfigId, cancellationToken);

        return ToDetailDto(c, categories, deityNames, log, setting);
    }

    public async Task<LogCountResultDto> LogCountAsync(LogCountDto dto, CancellationToken cancellationToken = default)
    {
        var userId = UserId;
        var today = Today;

        var chant = await _uow.ChantConfigs.GetDetailAsync(dto.ChantConfigId, cancellationToken)
            ?? throw new NotFoundException($"Chant '{dto.ChantConfigId}' was not found.");
        var categories = (await _uow.Chants.GetAllOrderedAsync(cancellationToken)).ToDictionary(c => c.Id, c => c);
        var deities = await _uow.Deities.ListWithoutImageAsync(cancellationToken);
        var deityNames = deities.ToDictionary(d => d.Id, d => d.Name);

        var setting = await _uow.Sadhana.GetChantSettingAsync(userId, dto.ChantConfigId, cancellationToken);
        var target = setting?.MalaSize ?? TargetCount(chant, categories);
        // Clamp to a sane per-day range: floor at 0, cap so a client can't inflate malas/streak
        // with an absurd count (also avoids integer-overflow in the lifetime totals).
        var newTotal = Math.Clamp(dto.TotalCount, 0, 1_000_000);

        var log = await _uow.Sadhana.GetLogAsync(userId, today, dto.ChantConfigId, cancellationToken);
        int oldMalas = log?.MalasCompleted ?? 0;
        bool isNewLog = log is null;

        if (isNewLog)
        {
            log = new SadhanaLog
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Date = today,
                ChantConfigId = dto.ChantConfigId,
                ChantName = chant.Name,
                DeityName = ResolveDeityNames(chant.DeityIds, deityNames).FirstOrDefault(),
                CategoryName = chant.Chant?.Name,
                WasRecommended = false
            };
            await _uow.Sadhana.AddAsync(log, cancellationToken);
        }

        log!.TargetCount = target;

        // Completed malas are banked: a lower total (the screen's Reset, which clears only the
        // mala in progress) can pull the count back to where that mala began, never below it.
        newTotal = Math.Max(newTotal, log.MalaStartCount);
        log.TotalCount = newTotal;

        // Only the mala in progress is measured against the current size, so changing the size
        // mid-day neither invents malas nor takes back ones already done.
        var inMala = newTotal - log.MalaStartCount;
        var gained = target > 0 ? inMala / target : 0;
        log.MalasCompleted += gained;
        log.MalaStartCount += gained * target;

        // A new log is already tracked as Added; calling Update would flip it to Modified
        // and EF would issue an UPDATE for a row that does not exist yet.
        if (!isNewLog) _uow.Sadhana.Update(log);

        int newMalas = log.MalasCompleted;
        bool malaCompleted = newMalas > oldMalas;

        // The day counts toward the streak once the user has completed at least one mala.
        int todayMalasTotal = (await _uow.Sadhana.GetLogsForDateAsync(userId, today, cancellationToken))
            .Where(l => l.ChantConfigId != log.ChantConfigId).Sum(l => l.MalasCompleted) + newMalas;

        var (streak, streakSecuredToday) = await UpdateStreakAsync(userId, today, todayMalasTotal, Math.Max(0, newMalas - oldMalas), cancellationToken);

        await _uow.SaveChangesAsync(cancellationToken);

        return new LogCountResultDto
        {
            Session = ToSession(log),
            Streak = ToStreakDto(streak, today),
            MalaCompleted = malaCompleted,
            StreakSecuredToday = streakSecuredToday
        };
    }

    public async Task<SadhanaStreakDto> GetStreakAsync(CancellationToken cancellationToken = default)
    {
        var streak = await _uow.Sadhana.GetStreakAsync(UserId, cancellationToken);
        return ToStreakDto(streak, Today);
    }

    // ---------- streak ----------

    private async Task<(SadhanaStreak Streak, bool SecuredToday)> UpdateStreakAsync(
        Guid userId, DateOnly today, int todayMalasTotal, int malasGained, CancellationToken cancellationToken)
    {
        var streak = await _uow.Sadhana.GetStreakAsync(userId, cancellationToken);
        bool isNew = streak is null;
        streak ??= new SadhanaStreak { Id = Guid.NewGuid(), UserId = userId };

        streak.TotalMalas += malasGained;

        bool securedToday = false;
        bool alreadyCountedToday = streak.LastPracticeDate == today;

        if (todayMalasTotal >= 1 && !alreadyCountedToday)
        {
            // first mala of the day -> the day now counts
            if (streak.LastPracticeDate == today.AddDays(-1))
                streak.CurrentStreak += 1;              // consecutive day
            else
                streak.CurrentStreak = 1;               // first ever, or a day was missed -> reset

            streak.LastPracticeDate = today;
            streak.LongestStreak = Math.Max(streak.LongestStreak, streak.CurrentStreak);
            streak.TotalDaysPracticed += 1;
            securedToday = true;
        }

        if (isNew) await _uow.Sadhana.AddStreakAsync(streak, cancellationToken);
        else _uow.Sadhana.UpdateStreak(streak);

        return (streak, securedToday);
    }

    // ---------- mapping ----------

    private void AddRecommendations(
        List<SadhanaChantDto> into,
        IEnumerable<Deity> matchedDeities,
        List<ChantConfig> chants,
        Dictionary<Guid, Chant> categories,
        Dictionary<Guid, string> deityNames,
        Dictionary<Guid, SadhanaLog> progress,
        string reasonPrefix)
    {
        var seen = into.Select(r => r.Id).ToHashSet();
        foreach (var deity in matchedDeities)
        {
            foreach (var chant in chants.Where(c => CsvIds.Split(c.DeityIds).Contains(deity.Id)))
            {
                if (!seen.Add(chant.Id)) continue;
                into.Add(ToChantDto(chant, categories, deityNames, progress, $"{reasonPrefix} · {deity.Name}"));
            }
        }
    }

    private static SadhanaChantDto ToChantDto(
        ChantConfig c,
        Dictionary<Guid, Chant> categories,
        Dictionary<Guid, string> deityNames,
        Dictionary<Guid, SadhanaLog> progress,
        string? reason)
    {
        progress.TryGetValue(c.Id, out var log);
        return new SadhanaChantDto
        {
            Id = c.Id,
            Name = c.Name,
            Description = c.Description,
            CategoryId = c.ChantId,
            CategoryName = c.Chant?.Name ?? string.Empty,
            DeityNames = ResolveDeityNames(c.DeityIds, deityNames),
            TextPreview = HtmlSanitizer.ToPlainText(c.ChantText) is { Length: > 0 } t
                ? (t.Length > 160 ? t[..160].TrimEnd() + "…" : t)
                : null,
            HasAudio = c.AudioContentType != null,
            TargetCount = TargetCount(c, categories),
            RecommendReason = reason,
            TodayCount = log?.TotalCount ?? 0,
            TodayMalas = log?.MalasCompleted ?? 0
        };
    }

    private static SadhanaSessionDto ToSession(SadhanaLog l) => new()
    {
        ChantConfigId = l.ChantConfigId,
        ChantName = l.ChantName,
        DeityName = l.DeityName,
        CategoryName = l.CategoryName,
        TargetCount = l.TargetCount,
        TotalCount = l.TotalCount,
        MalasCompleted = l.MalasCompleted,
        CountInMala = Math.Max(0, l.TotalCount - l.MalaStartCount)
    };

    private static SadhanaChantDetailDto ToDetailDto(
        ChantConfig c,
        Dictionary<Guid, Chant> categories,
        Dictionary<Guid, string> deityNames,
        SadhanaLog? log,
        UserChantSetting? setting)
    {
        var defaultTarget = TargetCount(c, categories);
        return new SadhanaChantDetailDto
        {
            Id = c.Id,
            Name = c.Name,
            Description = c.Description,
            CategoryId = c.ChantId,
            CategoryName = c.Chant?.Name ?? string.Empty,
            DeityNames = ResolveDeityNames(c.DeityIds, deityNames),
            ChantText = c.ChantText,
            HasAudio = c.AudioContentType != null,
            AudioFileName = c.AudioFileName,
            TargetCount = setting?.MalaSize ?? defaultTarget,
            DefaultTargetCount = defaultTarget,
            CustomTargetCount = setting?.MalaSize,
            VibrationEnabled = setting?.VibrationEnabled ?? true,
            TodayCount = log?.TotalCount ?? 0,
            TodayMalas = log?.MalasCompleted ?? 0,
            CountInMala = log is null ? 0 : Math.Max(0, log.TotalCount - log.MalaStartCount)
        };
    }

    /// <summary>The lists' "1 mala = N" follows the seeker's own size where they have set one.</summary>
    private async Task ApplyOwnMalaSizesAsync(List<SadhanaChantDto> chants, CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not { } userId || chants.Count == 0) return;

        var sizes = (await _uow.Sadhana.GetChantSettingsAsync(userId, cancellationToken))
            .Where(s => s.MalaSize is not null)
            .ToDictionary(s => s.ChantConfigId, s => s.MalaSize!.Value);
        if (sizes.Count == 0) return;

        foreach (var c in chants)
            if (sizes.TryGetValue(c.Id, out var size)) c.TargetCount = size;
    }

    private static SadhanaStreakDto ToStreakDto(SadhanaStreak? s, DateOnly today)
    {
        if (s is null) return new SadhanaStreakDto();

        // A streak stays "alive" only if the last practice was today or yesterday.
        bool alive = s.LastPracticeDate == today || s.LastPracticeDate == today.AddDays(-1);
        return new SadhanaStreakDto
        {
            CurrentStreak = alive ? s.CurrentStreak : 0,
            LongestStreak = s.LongestStreak,
            LastPracticeDate = s.LastPracticeDate,
            TotalMalas = s.TotalMalas,
            TotalDaysPracticed = s.TotalDaysPracticed,
            PracticedToday = s.LastPracticeDate == today
        };
    }

    private static int TargetCount(ChantConfig c, Dictionary<Guid, Chant> categories)
    {
        if (categories.TryGetValue(c.ChantId, out var cat) && cat.HasCount && cat.Count is > 0)
            return cat.Count.Value;
        return DefaultMala;
    }

    /// <summary>Deities are mapped to regions by NAME, so the chosen region id has to be resolved first.</summary>
    private async Task<string?> ResolveRegionNameAsync(Guid? regionId, CancellationToken cancellationToken)
    {
        if (regionId is null) return null;
        var region = await _uow.Regions.GetByIdAsync(regionId.Value, cancellationToken);
        return region?.Name;
    }

    private static List<string> ResolveDeityNames(string? csvIds, Dictionary<Guid, string> names)
        => CsvIds.Split(csvIds).Where(names.ContainsKey).Select(id => names[id]).ToList();

    private static bool MatchesSearch(ChantConfig c, string term, Dictionary<Guid, string> deityNames)
    {
        bool Has(string? text) => text?.Contains(term, StringComparison.OrdinalIgnoreCase) == true;

        return Has(c.Name)
            || Has(c.Description)
            || Has(c.TimeDescription)
            || Has(c.Chant?.Name)
            || ResolveDeityNames(c.DeityIds, deityNames).Any(Has);
    }

    private static List<string> SplitNames(string? csv)
        => string.IsNullOrWhiteSpace(csv)
            ? new()
            : csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
}
