using FluentValidation;
using Sanathana.Companion.Application.Common;
using Sanathana.Companion.Application.DTOs.Regions;
using Sanathana.Companion.Application.Interfaces;
using Sanathana.Companion.Domain.Entities;
using Sanathana.Companion.Domain.Exceptions;
using Sanathana.Companion.Domain.Interfaces;

namespace Sanathana.Companion.Application.Services;

public class RegionService : IRegionService
{
    private readonly IUnitOfWork _uow;
    private readonly IValidator<CreateRegionDto> _createValidator;
    private readonly IValidator<UpdateRegionDto> _updateValidator;

    public RegionService(
        IUnitOfWork uow,
        IValidator<CreateRegionDto> createValidator,
        IValidator<UpdateRegionDto> updateValidator)
    {
        _uow = uow;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    public async Task<IReadOnlyList<RegionDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var items = await _uow.Regions.GetAllOrderedAsync(cancellationToken);
        var languages = await _uow.Languages.GetAllOrderedAsync(cancellationToken);
        return items.Select(r => ToDto(r, languages)).ToList();
    }

    public async Task<IReadOnlyList<RegionOptionDto>> GetOptionsAsync(CancellationToken cancellationToken = default)
        => (await _uow.Regions.GetAllOrderedAsync(cancellationToken))
            .Where(r => r.IsActive)
            .Select(r => new RegionOptionDto { Id = r.Id, Name = r.Name })
            .ToList();

    public async Task<RegionDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _uow.Regions.GetByIdAsync(id, cancellationToken);
        if (entity is null) return null;
        var languages = await _uow.Languages.GetAllOrderedAsync(cancellationToken);
        return ToDto(entity, languages);
    }

    public async Task<Guid> CreateAsync(CreateRegionDto dto, CancellationToken cancellationToken = default)
    {
        await _createValidator.ValidateAndThrowAsync(dto, cancellationToken);

        var name = dto.Name.Trim();
        if (await _uow.Regions.NameExistsAsync(name, null, cancellationToken))
            throw new ConflictException($"A region named '{name}' already exists.");

        var entity = new Region
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = Clean(dto.Description),
            Latitude = dto.Latitude,
            Longitude = dto.Longitude,
            IsActive = dto.IsActive
        };

        await _uow.Regions.AddAsync(entity, cancellationToken);
        await SyncLanguagesAsync(entity.Id, dto.LanguageIds, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }

    public async Task UpdateAsync(Guid id, UpdateRegionDto dto, CancellationToken cancellationToken = default)
    {
        await _updateValidator.ValidateAndThrowAsync(dto, cancellationToken);

        var entity = await _uow.Regions.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Region '{id}' was not found.");

        var name = dto.Name.Trim();
        if (await _uow.Regions.NameExistsAsync(name, id, cancellationToken))
            throw new ConflictException($"A region named '{name}' already exists.");

        entity.Name = name;
        entity.Description = Clean(dto.Description);
        entity.Latitude = dto.Latitude;
        entity.Longitude = dto.Longitude;
        entity.IsActive = dto.IsActive;

        _uow.Regions.Update(entity);
        await SyncLanguagesAsync(id, dto.LanguageIds, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);
    }

    public async Task SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken = default)
    {
        var entity = await _uow.Regions.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Region '{id}' was not found.");

        entity.IsActive = isActive;
        _uow.Regions.Update(entity);
        // Deactivating a region deliberately keeps its language links, so reactivating restores them.
        await _uow.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Writes the region ↔ language relationship from the region side. There is only one copy
    /// of this relationship — the Languages.Regions column — so editing from the Region master
    /// and from the Languages master can never fall out of step.
    /// </summary>
    private async Task SyncLanguagesAsync(Guid regionId, List<Guid> languageIds, CancellationToken cancellationToken)
    {
        var selected = languageIds.Where(x => x != Guid.Empty).Distinct().ToHashSet();
        var all = await _uow.Languages.GetAllOrderedAsync(cancellationToken);

        var known = all.Select(l => l.Id).ToHashSet();
        if (selected.Any(id => !known.Contains(id)))
            throw new BadRequestException("One or more selected languages do not exist.");

        foreach (var language in all)
        {
            var shouldHave = selected.Contains(language.Id);

            // The region form only offers ACTIVE languages, so an inactive language could never
            // have been deselected on purpose — never strip its link.
            if (!language.IsActive && !shouldHave) continue;

            var regionIds = CsvIds.Split(language.Regions);
            var hasNow = regionIds.Contains(regionId);
            if (hasNow == shouldHave) continue;

            if (shouldHave) regionIds.Add(regionId);
            else regionIds.Remove(regionId);

            language.Regions = CsvIds.Join(regionIds);
            _uow.Languages.Update(language);
        }
    }

    private static RegionDto ToDto(Region r, IReadOnlyList<Language> languages)
    {
        var mapped = languages
            .Where(l => l.IsActive && CsvIds.Split(l.Regions).Contains(r.Id))
            .OrderBy(l => l.Name)
            .ToList();

        return new RegionDto
        {
            Id = r.Id,
            Name = r.Name,
            Description = r.Description,
            Latitude = r.Latitude,
            Longitude = r.Longitude,
            LanguageIds = mapped.Select(l => l.Id).ToList(),
            LanguageNames = mapped.Select(l => l.Name).ToList(),
            IsActive = r.IsActive
        };
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
