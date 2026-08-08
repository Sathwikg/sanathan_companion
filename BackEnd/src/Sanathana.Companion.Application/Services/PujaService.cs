using FluentValidation;
using Sanathana.Companion.Application.DTOs.Pujas;
using Sanathana.Companion.Application.Interfaces;
using Sanathana.Companion.Domain.Entities;
using Sanathana.Companion.Domain.Exceptions;
using Sanathana.Companion.Domain.Interfaces;

namespace Sanathana.Companion.Application.Services;

public class PujaService : IPujaService
{
    private const int MaxNameLength = 150;
    private const int MaxDescriptionLength = 1000;

    private readonly IUnitOfWork _uow;

    public PujaService(IUnitOfWork uow) => _uow = uow;

    public async Task<IReadOnlyList<PujaDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var rows = await _uow.Pujas.GetAllWithLinksAsync(cancellationToken);
        return rows.Select(Map).ToList();
    }

    public async Task<PujaDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _uow.Pujas.GetByIdAsync(id, cancellationToken);
        if (entity is null) return null;

        if (entity.FestivalId is { } fid)
            entity.Festival ??= await _uow.Festivals.GetByIdAsync(fid, cancellationToken);
        if (entity.DeityId is { } did)
            entity.Deity ??= await _uow.Deities.GetByIdAsync(did, cancellationToken);

        return Map(entity);
    }

    public async Task<PujaFormOptionsDto> GetFormOptionsAsync(CancellationToken cancellationToken = default)
    {
        var festivals = await _uow.Festivals.ListAllAsync(cancellationToken);
        var deities = await _uow.Deities.ListWithoutImageAsync(cancellationToken);

        return new PujaFormOptionsDto
        {
            Festivals = festivals
                .Where(f => f.IsActive)
                .OrderByDescending(f => f.Year).ThenBy(f => f.Name)
                .Select(f => new PujaFestivalOptionDto { Id = f.Id, Name = f.Name, Year = f.Year })
                .ToList(),

            Deities = deities
                .Where(d => d.IsActive)
                .OrderBy(d => d.Name)
                .Select(d => new PujaDeityOptionDto { Id = d.Id, Name = d.Name })
                .ToList()
        };
    }

    public async Task<Guid> CreateAsync(CreatePujaDto dto, CancellationToken cancellationToken = default)
    {
        var name = Require(dto.Name);
        var festivalId = Normalise(dto.FestivalId);
        var deityId = Normalise(dto.DeityId);

        await EnsureFestivalExistsAsync(festivalId, cancellationToken);
        await EnsureDeityExistsAsync(deityId, cancellationToken);
        await EnsureNameFreeAsync(name, festivalId, null, cancellationToken);

        var entity = new Puja
        {
            Name = name,
            Description = Trim(dto.Description, MaxDescriptionLength),
            FestivalId = festivalId,
            DeityId = deityId,
            IsActive = dto.IsActive
        };

        await _uow.Pujas.AddAsync(entity, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }

    public async Task UpdateAsync(Guid id, UpdatePujaDto dto, CancellationToken cancellationToken = default)
    {
        var entity = await _uow.Pujas.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Puja '{id}' was not found.");

        var name = Require(dto.Name);
        var festivalId = Normalise(dto.FestivalId);
        var deityId = Normalise(dto.DeityId);

        await EnsureFestivalExistsAsync(festivalId, cancellationToken);
        await EnsureDeityExistsAsync(deityId, cancellationToken);
        await EnsureNameFreeAsync(name, festivalId, id, cancellationToken);

        entity.Name = name;
        entity.Description = Trim(dto.Description, MaxDescriptionLength);
        entity.FestivalId = festivalId;
        entity.DeityId = deityId;
        entity.IsActive = dto.IsActive;

        _uow.Pujas.Update(entity);
        await _uow.SaveChangesAsync(cancellationToken);
    }

    public async Task SetStatusAsync(Guid id, bool isActive, CancellationToken cancellationToken = default)
    {
        var entity = await _uow.Pujas.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Puja '{id}' was not found.");

        entity.IsActive = isActive;
        _uow.Pujas.Update(entity);
        await _uow.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// An all-zero Guid is what an unset dropdown sends; treat it as "not mapped" rather than
    /// letting it reach the database and fail the foreign key.
    /// </summary>
    private static Guid? Normalise(Guid? id) => id == Guid.Empty ? null : id;

    private async Task EnsureNameFreeAsync(string name, Guid? festivalId, Guid? excludeId, CancellationToken ct)
    {
        if (!await _uow.Pujas.NameExistsAsync(name, festivalId, excludeId, ct)) return;

        throw new ConflictException(festivalId is null
            ? $"A puja named '{name}' already exists without a festival."
            : $"A puja named '{name}' already exists for this festival.");
    }

    private async Task EnsureFestivalExistsAsync(Guid? festivalId, CancellationToken cancellationToken)
    {
        if (festivalId is null) return;   // optional mapping

        _ = await _uow.Festivals.GetByIdAsync(festivalId.Value, cancellationToken)
            ?? throw new NotFoundException($"Festival '{festivalId}' was not found.");
    }

    private async Task EnsureDeityExistsAsync(Guid? deityId, CancellationToken cancellationToken)
    {
        if (deityId is null) return;      // optional mapping

        _ = await _uow.Deities.GetByIdAsync(deityId.Value, cancellationToken)
            ?? throw new NotFoundException($"Deity '{deityId}' was not found.");
    }

    private static string Require(string? name)
    {
        var trimmed = name?.Trim();
        if (string.IsNullOrEmpty(trimmed))
            throw new ValidationException("Puja name is required.");

        return trimmed.Length <= MaxNameLength ? trimmed : trimmed[..MaxNameLength];
    }

    private static string? Trim(string? value, int max)
    {
        var t = value?.Trim();
        if (string.IsNullOrEmpty(t)) return null;
        return t.Length <= max ? t : t[..max];
    }

    private static PujaDto Map(Puja p) => new()
    {
        Id = p.Id,
        Name = p.Name,
        Description = p.Description,
        FestivalId = p.FestivalId,
        FestivalName = p.Festival?.Name,
        FestivalYear = p.Festival?.Year,
        DeityId = p.DeityId,
        DeityName = p.Deity?.Name,
        IsActive = p.IsActive
    };
}
