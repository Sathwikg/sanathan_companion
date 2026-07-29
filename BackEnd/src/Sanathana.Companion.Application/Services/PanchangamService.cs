using Sanathana.Companion.Application.DTOs.Panchangams;
using Sanathana.Companion.Application.Interfaces;
using Sanathana.Companion.Application.Panchangam;
using Sanathana.Companion.Domain.Exceptions;
using Sanathana.Companion.Domain.Interfaces;

// The entity and the calculator namespace share the name "Panchangam"; alias the entity.
using PanchangamEntity = Sanathana.Companion.Domain.Entities.Panchangam;

namespace Sanathana.Companion.Application.Services;

public class PanchangamService : IPanchangamService
{
    private readonly IUnitOfWork _uow;

    public PanchangamService(IUnitOfWork uow) => _uow = uow;

    public async Task<IReadOnlyList<PanchangamDto>> GetAllAsync(
        int? year, Guid? regionId, DateOnly? from, DateOnly? to, string? search,
        CancellationToken cancellationToken = default)
    {
        var items = await _uow.Panchangams.GetFilteredAsync(year, regionId, from, to, search, cancellationToken);
        return items.Select(ToDto).ToList();
    }

    public async Task<PanchangamDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var p = await _uow.Panchangams.GetByIdAsync(id, cancellationToken);
        return p is null ? null : ToDto(p);
    }

    public async Task<PanchangamDto?> GetByDateAsync(DateOnly date, Guid regionId, CancellationToken cancellationToken = default)
    {
        var p = await _uow.Panchangams.GetByDateAsync(date, regionId, cancellationToken);
        return p is null ? null : ToDto(p);
    }

    public Task<PanchangamDto> ComputeAtLocationAsync(DateOnly date, double latitude, double longitude, string? placeLabel = null, CancellationToken cancellationToken = default)
    {
        if (latitude is < -90 or > 90) throw new BadRequestException("Latitude must be between -90 and 90.");
        if (longitude is < -180 or > 180) throw new BadRequestException("Longitude must be between -180 and 180.");

        var day = PanchangamCalculator.Compute(date, latitude, longitude);
        var dto = ToDto(day);
        dto.Latitude = latitude;
        dto.Longitude = longitude;
        dto.PlaceLabel = string.IsNullOrWhiteSpace(placeLabel) ? $"{latitude:F4}, {longitude:F4}" : placeLabel.Trim();
        dto.IsComputed = true;
        return Task.FromResult(dto);
    }

    public async Task<IReadOnlyList<int>> GetStoredYearsAsync(CancellationToken cancellationToken = default)
        => await _uow.Panchangams.GetYearsAsync(cancellationToken);

    public async Task<IReadOnlyList<PanchangamRegionOptionDto>> GetRegionOptionsAsync(CancellationToken cancellationToken = default)
    {
        var regions = await _uow.Regions.GetAllOrderedAsync(cancellationToken);
        return regions.Where(r => r.IsActive).Select(r => new PanchangamRegionOptionDto
        {
            Id = r.Id,
            Name = r.Name,
            Latitude = r.Latitude,
            Longitude = r.Longitude,
            HasCoordinates = r.Latitude.HasValue && r.Longitude.HasValue
        }).ToList();
    }

    public async Task<GenerateResultDto> GenerateAsync(GeneratePanchangamDto dto, CancellationToken cancellationToken = default)
    {
        if (dto.Year < 1900 || dto.Year > 2100)
            throw new BadRequestException("Year must be between 1900 and 2100.");

        var regions = (await _uow.Regions.GetAllOrderedAsync(cancellationToken)).Where(r => r.IsActive).ToList();
        if (dto.RegionId is not null)
            regions = regions.Where(r => r.Id == dto.RegionId).ToList();

        var result = new GenerateResultDto();
        var from = new DateOnly(dto.Year, 1, 1);
        var to = new DateOnly(dto.Year, 12, 31);

        foreach (var region in regions)
        {
            if (region.Latitude is null || region.Longitude is null)
            {
                result.Warnings.Add($"Skipped '{region.Name}' — no coordinates set.");
                continue;
            }

            result.Regions.Add(region.Name);
            var existing = await _uow.Panchangams.GetExistingDatesAsync(region.Id, from, to, cancellationToken);
            var toAdd = new List<PanchangamEntity>();

            for (var d = from; d <= to; d = d.AddDays(1))
            {
                bool exists = existing.Contains(d);
                if (exists && !dto.Overwrite) { result.Skipped++; continue; }

                var day = PanchangamCalculator.Compute(d, region.Latitude.Value, region.Longitude.Value);

                if (exists)
                {
                    var row = await _uow.Panchangams.GetByDateAsync(d, region.Id, cancellationToken);
                    // GetByDate returns a no-tracking copy; fetch the tracked entity to update
                    var tracked = await _uow.Panchangams.GetByIdAsync(row!.Id, cancellationToken);
                    Apply(tracked!, day, region.Id);
                    _uow.Panchangams.Update(tracked!);
                    result.Updated++;
                }
                else
                {
                    var entity = new PanchangamEntity { Id = Guid.NewGuid(), RegionId = region.Id };
                    Apply(entity, day, region.Id);
                    toAdd.Add(entity);
                    result.Created++;
                }
            }

            if (toAdd.Count > 0)
                await _uow.Panchangams.AddRangeAsync(toAdd, cancellationToken);

            // Commit per region to keep the change-tracker and transaction size bounded.
            await _uow.SaveChangesAsync(cancellationToken);
        }

        return result;
    }

    private static void Apply(PanchangamEntity entity, PanchangamDay day, Guid regionId)
    {
        entity.RegionId = regionId;
        entity.Date = day.Date;
        entity.Year = day.Date.Year;
        entity.DayOfWeek = day.DayOfWeek;
        entity.TeluguSamvatsaram = day.Samvatsaram;
        entity.Ayanam = day.Ayanam;
        entity.SakaSamvatsaram = day.SakaYear;
        entity.VikramaSamvatsaram = day.VikramaYear;
        entity.Masam = day.Masam;
        entity.Paksham = day.Paksham;
        entity.Rutuvu = day.Rutuvu;
        entity.Sunrise = day.Sunrise;
        entity.Sunset = day.Sunset;
        entity.TithiDetails = day.TithiFormatted;
        entity.NakshatramDetails = day.NakshatramFormatted;
        entity.AmruthaKalam = day.AmruthaKalam;
        entity.AbhijitMuhurtham = day.AbhijitMuhurtham;
        entity.Durmuhurtham = day.Durmuhurtham;
        entity.RahuKalam = day.RahuKalam;
        entity.Yamagandam = day.Yamagandam;
        entity.Varjyam = day.Varjyam;
        entity.Gulika = day.Gulika;
        entity.IsActive = true;
    }

    private static PanchangamDto ToDto(PanchangamEntity p) => new()
    {
        Id = p.Id,
        Date = p.Date,
        Year = p.Year,
        RegionId = p.RegionId,
        RegionName = p.Region?.Name,
        DayOfWeek = p.DayOfWeek,
        TeluguSamvatsaram = p.TeluguSamvatsaram,
        Ayanam = p.Ayanam,
        SakaSamvatsaram = p.SakaSamvatsaram,
        VikramaSamvatsaram = p.VikramaSamvatsaram,
        Masam = p.Masam,
        Paksham = p.Paksham,
        Rutuvu = p.Rutuvu,
        Sunrise = p.Sunrise,
        Sunset = p.Sunset,
        TithiDetails = p.TithiDetails,
        NakshatramDetails = p.NakshatramDetails,
        AmruthaKalam = p.AmruthaKalam,
        AbhijitMuhurtham = p.AbhijitMuhurtham,
        Durmuhurtham = p.Durmuhurtham,
        RahuKalam = p.RahuKalam,
        Yamagandam = p.Yamagandam,
        Varjyam = p.Varjyam,
        Gulika = p.Gulika,
        IsActive = p.IsActive
    };

    private static PanchangamDto ToDto(PanchangamDay d) => new()
    {
        Date = d.Date,
        Year = d.Date.Year,
        DayOfWeek = d.DayOfWeek,
        TeluguSamvatsaram = d.Samvatsaram,
        Ayanam = d.Ayanam,
        SakaSamvatsaram = d.SakaYear,
        VikramaSamvatsaram = d.VikramaYear,
        Masam = d.Masam,
        Paksham = d.Paksham,
        Rutuvu = d.Rutuvu,
        Sunrise = d.Sunrise,
        Sunset = d.Sunset,
        TithiDetails = d.TithiFormatted,
        NakshatramDetails = d.NakshatramFormatted,
        AmruthaKalam = d.AmruthaKalam,
        AbhijitMuhurtham = d.AbhijitMuhurtham,
        Durmuhurtham = d.Durmuhurtham,
        RahuKalam = d.RahuKalam,
        Yamagandam = d.Yamagandam,
        Varjyam = d.Varjyam,
        Gulika = d.Gulika,
        IsActive = true
    };
}
