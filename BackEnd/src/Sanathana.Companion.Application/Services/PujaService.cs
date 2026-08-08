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
        var rows = await _uow.Pujas.GetAllWithFestivalAsync(cancellationToken);
        return rows.Select(Map).ToList();
    }

    public async Task<PujaDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _uow.Pujas.GetByIdAsync(id, cancellationToken);
        if (entity is null) return null;

        entity.Festival ??= await _uow.Festivals.GetByIdAsync(entity.FestivalId, cancellationToken);
        return Map(entity);
    }

    public async Task<PujaFormOptionsDto> GetFormOptionsAsync(CancellationToken cancellationToken = default)
    {
        var festivals = await _uow.Festivals.ListAllAsync(cancellationToken);

        return new PujaFormOptionsDto
        {
            Festivals = festivals
                .Where(f => f.IsActive)
                .OrderByDescending(f => f.Year).ThenBy(f => f.Name)
                .Select(f => new PujaFestivalOptionDto { Id = f.Id, Name = f.Name, Year = f.Year })
                .ToList()
        };
    }

    public async Task<Guid> CreateAsync(CreatePujaDto dto, CancellationToken cancellationToken = default)
    {
        var name = Require(dto.Name);
        await EnsureFestivalExistsAsync(dto.FestivalId, cancellationToken);

        if (await _uow.Pujas.NameExistsAsync(name, dto.FestivalId, null, cancellationToken))
            throw new ConflictException($"A puja named '{name}' already exists for this festival.");

        var entity = new Puja
        {
            Name = name,
            Description = Trim(dto.Description, MaxDescriptionLength),
            FestivalId = dto.FestivalId,
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
        await EnsureFestivalExistsAsync(dto.FestivalId, cancellationToken);

        if (await _uow.Pujas.NameExistsAsync(name, dto.FestivalId, id, cancellationToken))
            throw new ConflictException($"A puja named '{name}' already exists for this festival.");

        entity.Name = name;
        entity.Description = Trim(dto.Description, MaxDescriptionLength);
        entity.FestivalId = dto.FestivalId;
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

    private async Task EnsureFestivalExistsAsync(Guid festivalId, CancellationToken cancellationToken)
    {
        if (festivalId == Guid.Empty)
            throw new ValidationException("Choose the festival this puja belongs to.");

        _ = await _uow.Festivals.GetByIdAsync(festivalId, cancellationToken)
            ?? throw new NotFoundException($"Festival '{festivalId}' was not found.");
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
        FestivalName = p.Festival?.Name ?? string.Empty,
        FestivalYear = p.Festival?.Year ?? 0,
        FestivalDate = p.Festival?.Date ?? default,
        IsActive = p.IsActive
    };
}
