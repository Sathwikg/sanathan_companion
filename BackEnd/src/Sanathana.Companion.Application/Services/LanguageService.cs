using FluentValidation;
using Sanathana.Companion.Application.Common;
using Sanathana.Companion.Application.DTOs.Languages;
using Sanathana.Companion.Application.Interfaces;
using Sanathana.Companion.Domain.Entities;
using Sanathana.Companion.Domain.Exceptions;
using Sanathana.Companion.Domain.Interfaces;

namespace Sanathana.Companion.Application.Services;

public class LanguageService : ILanguageService
{
    private readonly IUnitOfWork _uow;
    private readonly IValidator<CreateLanguageDto> _createValidator;
    private readonly IValidator<UpdateLanguageDto> _updateValidator;

    public LanguageService(
        IUnitOfWork uow,
        IValidator<CreateLanguageDto> createValidator,
        IValidator<UpdateLanguageDto> updateValidator)
    {
        _uow = uow;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    public async Task<IReadOnlyList<LanguageDto>> GetAllAsync(Guid? regionId, string? search, CancellationToken cancellationToken = default)
    {
        var items = await _uow.Languages.GetFilteredAsync(regionId, search, cancellationToken);
        var regionNames = await GetRegionNamesAsync(cancellationToken);
        return items.Select(l => ToDto(l, regionNames)).ToList();
    }

    public async Task<LanguageDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var l = await _uow.Languages.GetByIdAsync(id, cancellationToken);
        if (l is null) return null;
        return ToDto(l, await GetRegionNamesAsync(cancellationToken));
    }

    public async Task<IReadOnlyList<RegionLanguagesDto>> GetByRegionAsync(CancellationToken cancellationToken = default)
    {
        var regions = (await _uow.Regions.GetAllOrderedAsync(cancellationToken)).Where(r => r.IsActive).ToList();
        var languages = await _uow.Languages.GetAllOrderedAsync(cancellationToken);

        return regions.Select(r => new RegionLanguagesDto
        {
            RegionId = r.Id,
            RegionName = r.Name,
            Languages = languages
                .Where(l => l.IsActive && SplitIds(l.Regions).Contains(r.Id))
                .Select(l => l.Name)
                .ToList()
        }).ToList();
    }

    public async Task<Guid> CreateAsync(CreateLanguageDto dto, CancellationToken cancellationToken = default)
    {
        await _createValidator.ValidateAndThrowAsync(dto, cancellationToken);

        var name = dto.Name.Trim();
        if (await _uow.Languages.NameExistsAsync(name, null, cancellationToken))
            throw new ConflictException($"A language named '{name}' already exists.");

        await EnsureRegionsExistAsync(dto.RegionIds, cancellationToken);

        var entity = new Language
        {
            Id = Guid.NewGuid(),
            Name = name,
            NativeName = Clean(dto.NativeName),
            Code = Clean(dto.Code)?.ToLowerInvariant(),
            Description = Clean(dto.Description),
            Regions = JoinIds(dto.RegionIds),
            IsActive = dto.IsActive
        };

        await _uow.Languages.AddAsync(entity, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }

    public async Task UpdateAsync(Guid id, UpdateLanguageDto dto, CancellationToken cancellationToken = default)
    {
        await _updateValidator.ValidateAndThrowAsync(dto, cancellationToken);

        var entity = await _uow.Languages.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Language '{id}' was not found.");

        var name = dto.Name.Trim();
        if (await _uow.Languages.NameExistsAsync(name, id, cancellationToken))
            throw new ConflictException($"A language named '{name}' already exists.");

        await EnsureRegionsExistAsync(dto.RegionIds, cancellationToken);

        entity.Name = name;
        entity.NativeName = Clean(dto.NativeName);
        entity.Code = Clean(dto.Code)?.ToLowerInvariant();
        entity.Description = Clean(dto.Description);
        entity.Regions = JoinIds(dto.RegionIds);
        entity.IsActive = dto.IsActive;

        _uow.Languages.Update(entity);
        await _uow.SaveChangesAsync(cancellationToken);
    }

    public async Task SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken = default)
    {
        var entity = await _uow.Languages.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Language '{id}' was not found.");
        entity.IsActive = isActive;
        _uow.Languages.Update(entity);
        await _uow.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureRegionsExistAsync(IEnumerable<Guid> regionIds, CancellationToken cancellationToken)
    {
        var ids = regionIds.Where(id => id != Guid.Empty).Distinct().ToList();
        if (ids.Count == 0) return;

        var known = (await _uow.Regions.GetAllOrderedAsync(cancellationToken)).Select(r => r.Id).ToHashSet();
        if (ids.Any(id => !known.Contains(id)))
            throw new BadRequestException("One or more selected regions do not exist.");
    }

    private async Task<Dictionary<Guid, string>> GetRegionNamesAsync(CancellationToken cancellationToken)
        => (await _uow.Regions.GetAllOrderedAsync(cancellationToken)).ToDictionary(r => r.Id, r => r.Name);

    private static LanguageDto ToDto(Language l, Dictionary<Guid, string> regionNames)
    {
        var ids = SplitIds(l.Regions);
        return new LanguageDto
        {
            Id = l.Id,
            Name = l.Name,
            NativeName = l.NativeName,
            Code = l.Code,
            Description = l.Description,
            RegionIds = ids,
            RegionNames = ids.Where(regionNames.ContainsKey).Select(id => regionNames[id]).ToList(),
            IsActive = l.IsActive
        };
    }

    // Shared with RegionService so both sides of the relationship parse it identically.
    private static string? JoinIds(IEnumerable<Guid> ids) => CsvIds.Join(ids);

    private static List<Guid> SplitIds(string? csv) => CsvIds.Split(csv);

    private static string? Clean(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
}
