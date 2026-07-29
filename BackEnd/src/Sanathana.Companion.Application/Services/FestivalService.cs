using FluentValidation;
using Sanathana.Companion.Application.DTOs.Festivals;
using Sanathana.Companion.Application.Interfaces;
using Sanathana.Companion.Domain.Entities;
using Sanathana.Companion.Domain.Exceptions;
using Sanathana.Companion.Domain.Interfaces;

namespace Sanathana.Companion.Application.Services;

public class FestivalService : IFestivalService
{
    private readonly IUnitOfWork _uow;
    private readonly IValidator<CreateFestivalDto> _createValidator;
    private readonly IValidator<UpdateFestivalDto> _updateValidator;

    public FestivalService(
        IUnitOfWork uow,
        IValidator<CreateFestivalDto> createValidator,
        IValidator<UpdateFestivalDto> updateValidator)
    {
        _uow = uow;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    public async Task<IReadOnlyList<FestivalDto>> GetByYearAsync(int year, CancellationToken cancellationToken = default)
    {
        var items = await _uow.Festivals.GetByYearAsync(year, cancellationToken);
        var regionNames = await GetRegionNamesAsync(cancellationToken);
        return items.Select(f => ToDto(f, regionNames)).ToList();
    }

    public Task<IReadOnlyList<int>> GetYearsAsync(CancellationToken cancellationToken = default)
        => _uow.Festivals.GetYearsAsync(cancellationToken);

    public async Task<FestivalDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _uow.Festivals.GetByIdAsync(id, cancellationToken);
        if (entity is null) return null;
        var regionNames = await GetRegionNamesAsync(cancellationToken);
        return ToDto(entity, regionNames);
    }

    public async Task<Guid> CreateAsync(CreateFestivalDto dto, CancellationToken cancellationToken = default)
    {
        await _createValidator.ValidateAndThrowAsync(dto, cancellationToken);

        var name = dto.Name.Trim();
        if (await _uow.Festivals.ExistsAsync(name, dto.Year, null, cancellationToken))
            throw new ConflictException($"A festival named '{name}' already exists for {dto.Year}.");

        var entity = new Festival
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = Clean(dto.Description),
            Year = dto.Year,
            Date = dto.Date,
            Regions = JoinIds(dto.RegionIds),
            IsActive = dto.IsActive
        };

        await _uow.Festivals.AddAsync(entity, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }

    public async Task UpdateAsync(Guid id, UpdateFestivalDto dto, CancellationToken cancellationToken = default)
    {
        await _updateValidator.ValidateAndThrowAsync(dto, cancellationToken);

        var entity = await _uow.Festivals.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Festival '{id}' was not found.");

        var name = dto.Name.Trim();
        if (await _uow.Festivals.ExistsAsync(name, dto.Year, id, cancellationToken))
            throw new ConflictException($"A festival named '{name}' already exists for {dto.Year}.");

        entity.Name = name;
        entity.Description = Clean(dto.Description);
        entity.Year = dto.Year;
        entity.Date = dto.Date;
        entity.Regions = JoinIds(dto.RegionIds);
        entity.IsActive = dto.IsActive;

        _uow.Festivals.Update(entity);
        await _uow.SaveChangesAsync(cancellationToken);
    }

    public async Task SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken = default)
    {
        var entity = await _uow.Festivals.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Festival '{id}' was not found.");

        entity.IsActive = isActive;
        _uow.Festivals.Update(entity);
        await _uow.SaveChangesAsync(cancellationToken);
    }

    private async Task<IReadOnlyDictionary<Guid, string>> GetRegionNamesAsync(CancellationToken cancellationToken)
    {
        var regions = await _uow.Regions.GetAllOrderedAsync(cancellationToken);
        return regions.ToDictionary(r => r.Id, r => r.Name);
    }

    private static FestivalDto ToDto(Festival f, IReadOnlyDictionary<Guid, string> regionNames)
    {
        var ids = ParseIds(f.Regions);
        return new FestivalDto
        {
            Id = f.Id,
            Name = f.Name,
            Description = f.Description,
            Year = f.Year,
            Date = f.Date,
            RegionIds = ids,
            RegionNames = ids.Where(regionNames.ContainsKey).Select(id => regionNames[id]).ToList(),
            IsActive = f.IsActive
        };
    }

    private static string? JoinIds(IEnumerable<Guid> ids)
    {
        var distinct = ids.Where(id => id != Guid.Empty).Distinct().ToList();
        return distinct.Count == 0 ? null : string.Join(",", distinct);
    }

    private static List<Guid> ParseIds(string? csv)
    {
        if (string.IsNullOrWhiteSpace(csv)) return new();
        return csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                  .Select(s => Guid.TryParse(s, out var g) ? g : (Guid?)null)
                  .Where(g => g.HasValue)
                  .Select(g => g!.Value)
                  .ToList();
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
