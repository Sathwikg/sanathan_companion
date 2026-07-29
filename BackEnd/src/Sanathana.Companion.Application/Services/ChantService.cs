using FluentValidation;
using Sanathana.Companion.Application.DTOs.Chants;
using Sanathana.Companion.Application.Interfaces;
using Sanathana.Companion.Domain.Entities;
using Sanathana.Companion.Domain.Exceptions;
using Sanathana.Companion.Domain.Interfaces;

namespace Sanathana.Companion.Application.Services;

public class ChantService : IChantService
{
    private readonly IUnitOfWork _uow;
    private readonly IValidator<CreateChantDto> _createValidator;
    private readonly IValidator<UpdateChantDto> _updateValidator;

    public ChantService(IUnitOfWork uow, IValidator<CreateChantDto> createValidator, IValidator<UpdateChantDto> updateValidator)
    {
        _uow = uow;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    public async Task<IReadOnlyList<ChantDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var items = await _uow.Chants.GetAllOrderedAsync(cancellationToken);
        return items.Select(ToDto).ToList();
    }

    public async Task<ChantDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var c = await _uow.Chants.GetByIdAsync(id, cancellationToken);
        return c is null ? null : ToDto(c);
    }

    public async Task<Guid> CreateAsync(CreateChantDto dto, CancellationToken cancellationToken = default)
    {
        await _createValidator.ValidateAndThrowAsync(dto, cancellationToken);

        var name = dto.Name.Trim();
        if (await _uow.Chants.NameExistsAsync(name, null, cancellationToken))
            throw new ConflictException($"A chant named '{name}' already exists.");

        var entity = new Chant
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = Clean(dto.Description),
            HasCount = dto.HasCount,
            Count = dto.HasCount ? dto.Count : null,
            IsActive = dto.IsActive
        };

        await _uow.Chants.AddAsync(entity, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }

    public async Task UpdateAsync(Guid id, UpdateChantDto dto, CancellationToken cancellationToken = default)
    {
        await _updateValidator.ValidateAndThrowAsync(dto, cancellationToken);

        var entity = await _uow.Chants.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Chant '{id}' was not found.");

        var name = dto.Name.Trim();
        if (await _uow.Chants.NameExistsAsync(name, id, cancellationToken))
            throw new ConflictException($"A chant named '{name}' already exists.");

        entity.Name = name;
        entity.Description = Clean(dto.Description);
        entity.HasCount = dto.HasCount;
        entity.Count = dto.HasCount ? dto.Count : null;
        entity.IsActive = dto.IsActive;

        _uow.Chants.Update(entity);
        await _uow.SaveChangesAsync(cancellationToken);
    }

    public async Task SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken = default)
    {
        var entity = await _uow.Chants.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Chant '{id}' was not found.");
        entity.IsActive = isActive;
        _uow.Chants.Update(entity);
        await _uow.SaveChangesAsync(cancellationToken);
    }

    private static ChantDto ToDto(Chant c) => new()
    {
        Id = c.Id,
        Name = c.Name,
        Description = c.Description,
        HasCount = c.HasCount,
        Count = c.Count,
        IsActive = c.IsActive
    };

    private static string? Clean(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
}
