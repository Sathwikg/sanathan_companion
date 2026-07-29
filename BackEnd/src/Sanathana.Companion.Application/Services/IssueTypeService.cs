using FluentValidation;
using Sanathana.Companion.Application.DTOs.IssueTypes;
using Sanathana.Companion.Application.Interfaces;
using Sanathana.Companion.Domain.Entities;
using Sanathana.Companion.Domain.Exceptions;
using Sanathana.Companion.Domain.Interfaces;

namespace Sanathana.Companion.Application.Services;

public class IssueTypeService : IIssueTypeService
{
    private readonly IUnitOfWork _uow;
    private readonly IValidator<CreateIssueTypeDto> _createValidator;
    private readonly IValidator<UpdateIssueTypeDto> _updateValidator;

    public IssueTypeService(
        IUnitOfWork uow,
        IValidator<CreateIssueTypeDto> createValidator,
        IValidator<UpdateIssueTypeDto> updateValidator)
    {
        _uow = uow;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    public async Task<IReadOnlyList<IssueTypeDto>> GetAllAsync(CancellationToken cancellationToken = default)
        => (await _uow.IssueTypes.GetAllOrderedAsync(cancellationToken)).Select(ToDto).ToList();

    public async Task<IReadOnlyList<IssueTypeDto>> GetActiveAsync(CancellationToken cancellationToken = default)
        => (await _uow.IssueTypes.GetAllOrderedAsync(cancellationToken)).Where(t => t.IsActive).Select(ToDto).ToList();

    public async Task<IssueTypeDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _uow.IssueTypes.GetByIdAsync(id, cancellationToken);
        return entity is null ? null : ToDto(entity);
    }

    public async Task<Guid> CreateAsync(CreateIssueTypeDto dto, CancellationToken cancellationToken = default)
    {
        await _createValidator.ValidateAndThrowAsync(dto, cancellationToken);

        var name = dto.Name.Trim();
        if (await _uow.IssueTypes.NameExistsAsync(name, null, cancellationToken))
            throw new ConflictException($"An issue type named '{name}' already exists.");

        var entity = new IssueType
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = Clean(dto.Description),
            DisplayOrder = dto.DisplayOrder,
            IsActive = dto.IsActive
        };

        await _uow.IssueTypes.AddAsync(entity, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }

    public async Task UpdateAsync(Guid id, UpdateIssueTypeDto dto, CancellationToken cancellationToken = default)
    {
        await _updateValidator.ValidateAndThrowAsync(dto, cancellationToken);

        var entity = await _uow.IssueTypes.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Issue type '{id}' was not found.");

        var name = dto.Name.Trim();
        if (await _uow.IssueTypes.NameExistsAsync(name, id, cancellationToken))
            throw new ConflictException($"An issue type named '{name}' already exists.");

        entity.Name = name;
        entity.Description = Clean(dto.Description);
        entity.DisplayOrder = dto.DisplayOrder;
        entity.IsActive = dto.IsActive;

        _uow.IssueTypes.Update(entity);
        await _uow.SaveChangesAsync(cancellationToken);
    }

    public async Task SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken = default)
    {
        var entity = await _uow.IssueTypes.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Issue type '{id}' was not found.");

        entity.IsActive = isActive;
        _uow.IssueTypes.Update(entity);
        await _uow.SaveChangesAsync(cancellationToken);
    }

    private static IssueTypeDto ToDto(IssueType t) => new()
    {
        Id = t.Id,
        Name = t.Name,
        Description = t.Description,
        DisplayOrder = t.DisplayOrder,
        IsActive = t.IsActive
    };

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
