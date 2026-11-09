using Sanathana.Companion.Application.DTOs.Calendar;
using Sanathana.Companion.Application.HinduCalendar;
using Sanathana.Companion.Application.Interfaces;
using Sanathana.Companion.Application.Panchangam;
using Sanathana.Companion.Domain.Entities;
using Sanathana.Companion.Domain.Exceptions;
using Sanathana.Companion.Domain.Interfaces;

using PanchangamEntity = Sanathana.Companion.Domain.Entities.Panchangam;

namespace Sanathana.Companion.Application.Services;

/// <summary>
/// Assembles a month of the Hindu calendar from four masters that were never joined before: the
/// stored Panchangam (tithi, nakshatra, moon), the Festivals master (by region), the Deities master
/// (which deity a festival honours, which deities a weekday belongs to) and a seeker's own marks.
/// </summary>
/// <remarks>
/// Stored rows are preferred and computed rows fill the gaps, through the same engine and the same
/// memo the compute endpoint uses, so a month never shows blanks while the coverage job is still
/// catching up on a fresh database. The two paths produce byte-identical text, which is what lets
/// <see cref="CalendarRules"/> read either.
/// </remarks>
public class CalendarService : ICalendarService
{
    private readonly IUnitOfWork _uow;
    private readonly IPanchangamComputeCache _compute;

    public CalendarService(IUnitOfWork uow, IPanchangamComputeCache compute)
    {
        _uow = uow;
        _compute = compute;
    }

    // ------------------------------------------------------------------ the month

    public async Task<CalendarMonthDto> GetMonthAsync(int year, int month, Guid? regionId, Guid? userId, CancellationToken cancellationToken = default)
    {
        if (year < 1900 || year > 2100) throw new BadRequestException("Year must be between 1900 and 2100.");
        if (month < 1 || month > 12) throw new BadRequestException("Month must be between 1 and 12.");

        var first = new DateOnly(year, month, 1);
        var last = first.AddMonths(1).AddDays(-1);

        var regions = (await _uow.Regions.GetAllOrderedAsync(cancellationToken)).Where(r => r.IsActive).ToList();
        var region = regionId is { } rid
            ? regions.FirstOrDefault(r => r.Id == rid) ?? throw new NotFoundException("Region not found.")
            : regions.FirstOrDefault(r => r.Latitude is not null && r.Longitude is not null) ?? regions.FirstOrDefault();

        var dto = new CalendarMonthDto
        {
            Year = year,
            Month = month,
            RegionId = region?.Id,
            RegionName = region?.Name
        };

        // ---- the Panchangam, stored first and computed for whatever is missing ----
        var stored = new Dictionary<DateOnly, PanchangamEntity>();
        var hasCoordinates = region is { Latitude: not null, Longitude: not null };
        if (region is not null)
        {
            var (rows, _) = await _uow.Panchangams.GetPagedAsync(null, region.Id, first, last, null, 1, 64, cancellationToken);
            foreach (var row in rows) stored[row.Date] = row;
        }

        // ---- festivals of the month for this region ----
        var festivals = (await _uow.Festivals.GetByYearAsync(year, cancellationToken))
            .Where(f => f.IsActive && f.Date >= first && f.Date <= last && FestivalCoversRegion(f, region?.Id))
            .OrderBy(f => f.Date).ThenBy(f => f.Name)
            .ToList();

        // ---- deities, once: by festival name and by weekday ----
        var deities = (await _uow.Deities.ListWithoutImageAsync(cancellationToken))
            .Where(d => d.IsActive && DeityCoversRegion(d, region?.Name))
            .ToList();

        var deitiesByFestival = new Dictionary<string, List<Deity>>(StringComparer.OrdinalIgnoreCase);
        var deitiesByWeekday = new Dictionary<string, List<Deity>>(StringComparer.OrdinalIgnoreCase);
        foreach (var deity in deities)
        {
            foreach (var name in Csv(deity.Festivals))
                (deitiesByFestival.TryGetValue(name, out var list) ? list : deitiesByFestival[name] = new()).Add(deity);
            foreach (var day in Csv(deity.Days))
                (deitiesByWeekday.TryGetValue(day, out var list) ? list : deitiesByWeekday[day] = new()).Add(deity);
        }

        // ---- the days ----
        var masams = new List<string>();
        for (var date = first; date <= last; date = date.AddDays(1))
        {
            var day = new CalendarDayDto { Date = date, DayOfWeek = PanchangamTables.WeekdaysEnglish[(int)date.DayOfWeek] };

            if (stored.TryGetValue(date, out var row))
            {
                Apply(day, row);
                dto.Samvatsaram ??= row.TeluguSamvatsaram;
            }
            else if (hasCoordinates)
            {
                var computed = _compute.GetOrCompute(date, region!.Latitude!.Value, region.Longitude!.Value);
                Apply(day, computed);
                dto.Samvatsaram ??= computed.Samvatsaram;
                dto.HasComputedDays = true;
            }

            if (day.Masam is { } masam && !masams.Contains(masam)) masams.Add(masam);

            foreach (var festival in festivals.Where(f => f.Date == date))
            {
                var f = new CalendarFestivalDto { Id = festival.Id, Name = festival.Name, Description = festival.Description };
                if (deitiesByFestival.TryGetValue(festival.Name.Trim(), out var honoured))
                    f.Deities = honoured.OrderBy(d => d.Name).Select(ToDeity).ToList();
                day.Festivals.Add(f);
            }

            if (deitiesByWeekday.TryGetValue(day.DayOfWeek, out var ofWeekday))
                day.Deities = ofWeekday.OrderBy(d => d.Name).Select(ToDeity).ToList();

            var assessment = CalendarRules.Assess(day.TithiIndex, day.NakshatraIndex, date.DayOfWeek, day.Festivals.Count > 0);
            day.Rating = CalendarRules.RatingCode(assessment.Rating);
            day.Score = assessment.Score;
            day.Reasons = assessment.Reasons.ToList();

            dto.Days.Add(day);
        }

        dto.Masams = masams;
        dto.MissingPanchangam = dto.Days.All(d => !d.HasPanchangam);

        // ---- the seeker's own marks ----
        if (userId is { } uid)
            dto.Events = await ExpandEventsAsync(uid, first, last, cancellationToken);

        return dto;
    }

    /// <summary>Null or empty Regions means the festival is observed everywhere.</summary>
    private static bool FestivalCoversRegion(Festival festival, Guid? regionId)
    {
        if (regionId is null || string.IsNullOrWhiteSpace(festival.Regions)) return true;
        var id = regionId.Value.ToString();
        return Csv(festival.Regions).Any(r => r.Equals(id, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Deity.Regions holds names, not ids; empty means every region.</summary>
    private static bool DeityCoversRegion(Deity deity, string? regionName)
    {
        if (regionName is null || string.IsNullOrWhiteSpace(deity.Regions)) return true;
        return Csv(deity.Regions).Any(r => r.Equals(regionName, StringComparison.OrdinalIgnoreCase));
    }

    private static IEnumerable<string> Csv(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? Enumerable.Empty<string>()
            : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static CalendarDeityDto ToDeity(Deity d) => new()
    {
        Id = d.Id,
        Name = d.Name,
        DeityType = d.DeityType,
        HasImage = !string.IsNullOrEmpty(d.ImageContentType)
    };

    private static void Apply(CalendarDayDto day, PanchangamEntity p)
        => Apply(day, p.TithiDetails, p.NakshatramDetails, p.Paksham, p.Masam, p.Sunrise, p.Sunset,
                 p.RahuKalam, p.Yamagandam, p.Gulika, p.Durmuhurtham, p.Varjyam, p.AmruthaKalam, p.AbhijitMuhurtham);

    private static void Apply(CalendarDayDto day, PanchangamDay p)
        => Apply(day, p.TithiFormatted, p.NakshatramFormatted, p.Paksham, p.Masam, p.Sunrise, p.Sunset,
                 p.RahuKalam, p.Yamagandam, p.Gulika, p.Durmuhurtham, p.Varjyam, p.AmruthaKalam, p.AbhijitMuhurtham);

    private static void Apply(CalendarDayDto day, string? tithi, string? nakshatram, string? paksham, string? masam,
        TimeOnly? sunrise, TimeOnly? sunset, string? rahu, string? yama, string? gulika, string? durmuhurtham,
        string? varjyam, string? amrutha, string? abhijit)
    {
        day.HasPanchangam = true;
        day.TithiDetails = tithi;
        day.NakshatramDetails = nakshatram;
        day.Paksham = paksham;
        day.Masam = masam;
        day.Sunrise = sunrise;
        day.Sunset = sunset;
        day.RahuKalam = rahu;
        day.Yamagandam = yama;
        day.Gulika = gulika;
        day.Durmuhurtham = durmuhurtham;
        day.Varjyam = varjyam;
        day.AmruthaKalam = amrutha;
        day.AbhijitMuhurtham = abhijit;

        if (CalendarRules.TryTithiIndex(tithi, paksham, out var t))
        {
            day.TithiIndex = t;
            day.Tithi = CalendarRules.TithiName(t);
            day.MoonPhase = CalendarRules.MoonPhase(t);
            day.MoonIllumination = CalendarRules.MoonIllumination(t);
            day.Markers = CalendarRules.Markers(t).ToList();
        }
        else
        {
            day.Tithi = CalendarRules.FirstName(tithi);
        }

        if (CalendarRules.TryNakshatraIndex(nakshatram, out var n))
        {
            day.NakshatraIndex = n;
            day.Nakshatram = PanchangamTables.Nakshatras[n];
        }
        else
        {
            day.Nakshatram = CalendarRules.FirstName(nakshatram);
        }
    }

    // ------------------------------------------------------------------ the seeker's marks

    public async Task<IReadOnlyList<CalendarEventDto>> GetEventsAsync(Guid userId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        if (to < from) throw new BadRequestException("The range end is before its start.");
        if (to.DayNumber - from.DayNumber > 400) throw new BadRequestException("Ask for at most a year at a time.");
        return await ExpandEventsAsync(userId, from, to, cancellationToken);
    }

    /// <summary>
    /// The marks inside a range, with every yearly repeat placed on its anniversary in each year
    /// the range touches. A 29 February repeat lands on 28 February in a common year.
    /// </summary>
    private async Task<List<CalendarEventDto>> ExpandEventsAsync(Guid userId, DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        var rows = await _uow.CalendarEvents.GetForRangeAsync(userId, from, to, cancellationToken);
        var result = new List<CalendarEventDto>();

        foreach (var row in rows)
        {
            if (!row.RepeatYearly)
            {
                result.Add(ToDto(row, row.Date));
                continue;
            }

            for (var year = from.Year; year <= to.Year; year++)
            {
                var day = Math.Min(row.Date.Day, DateTime.DaysInMonth(year, row.Date.Month));
                var occurs = new DateOnly(year, row.Date.Month, day);
                if (occurs < row.Date || occurs < from || occurs > to) continue;
                result.Add(ToDto(row, occurs));
            }
        }

        return result.OrderBy(e => e.OccursOn).ThenBy(e => e.Time).ThenBy(e => e.Title).ToList();
    }

    public async Task<CalendarEventDto> CreateEventAsync(Guid userId, SaveCalendarEventDto dto, CancellationToken cancellationToken = default)
    {
        var entity = new CalendarEvent { Id = Guid.NewGuid(), UserId = userId };
        Apply(entity, dto);
        await _uow.CalendarEvents.AddAsync(entity, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);
        return ToDto(entity, entity.Date);
    }

    public async Task<CalendarEventDto> UpdateEventAsync(Guid userId, Guid id, SaveCalendarEventDto dto, CancellationToken cancellationToken = default)
    {
        var entity = await _uow.CalendarEvents.GetOwnedAsync(id, userId, cancellationToken)
            ?? throw new NotFoundException("That mark was not found.");
        Apply(entity, dto);
        _uow.CalendarEvents.Update(entity);
        await _uow.SaveChangesAsync(cancellationToken);
        return ToDto(entity, entity.Date);
    }

    public async Task DeleteEventAsync(Guid userId, Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _uow.CalendarEvents.GetOwnedAsync(id, userId, cancellationToken)
            ?? throw new NotFoundException("That mark was not found.");
        _uow.CalendarEvents.Remove(entity);
        await _uow.SaveChangesAsync(cancellationToken);
    }

    /// <summary>The validator runs first on the API; these checks hold when the service is called directly.</summary>
    private static void Apply(CalendarEvent entity, SaveCalendarEventDto dto)
    {
        var title = dto.Title?.Trim() ?? string.Empty;
        if (title.Length == 0) throw new BadRequestException("Give the mark a title.");
        if (title.Length > CalendarEvent.MaxTitleLength) throw new BadRequestException("The title is too long.");
        if (dto.Date.Year < 1900 || dto.Date.Year > 2100) throw new BadRequestException("Date must be between 1900 and 2100.");

        var kind = CalendarEventKinds.Normalize(dto.Kind) ?? throw new BadRequestException("Unknown kind of mark.");
        var notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim();
        if (notes is { Length: > CalendarEvent.MaxNotesLength }) throw new BadRequestException("The notes are too long.");

        entity.Date = dto.Date;
        entity.Time = dto.Time;
        entity.Title = title;
        entity.Notes = notes;
        entity.Kind = kind;
        entity.RepeatYearly = dto.RepeatYearly;
    }

    private static CalendarEventDto ToDto(CalendarEvent e, DateOnly occursOn) => new()
    {
        Id = e.Id,
        Date = e.Date,
        OccursOn = occursOn,
        Time = e.Time,
        Title = e.Title,
        Notes = e.Notes,
        Kind = e.Kind,
        RepeatYearly = e.RepeatYearly
    };
}
