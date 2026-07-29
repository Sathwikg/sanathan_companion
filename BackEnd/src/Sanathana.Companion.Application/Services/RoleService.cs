using FluentValidation;
using Sanathana.Companion.Application.Common;
using Sanathana.Companion.Application.DTOs.Roles;
using Sanathana.Companion.Application.Interfaces;
using Sanathana.Companion.Domain.Entities;
using Sanathana.Companion.Domain.Exceptions;
using Sanathana.Companion.Domain.Interfaces;

namespace Sanathana.Companion.Application.Services;

public class RoleService : IRoleService
{
    private readonly IUnitOfWork _uow;
    private readonly IValidator<CreateRoleDto> _createValidator;
    private readonly IValidator<UpdateRoleDto> _updateValidator;

    public RoleService(
        IUnitOfWork uow,
        IValidator<CreateRoleDto> createValidator,
        IValidator<UpdateRoleDto> updateValidator)
    {
        _uow = uow;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    public async Task<IReadOnlyList<RoleDto>> GetAllAsync(string? search, CancellationToken cancellationToken = default)
    {
        var roles = await _uow.Roles.GetFilteredAsync(search, cancellationToken);
        var userCounts = await _uow.Roles.GetUserCountsAsync(cancellationToken);
        var formCounts = await _uow.ModuleRoleMappings.GetCountsByRoleAsync(cancellationToken);
        return roles.Select(r => ToDto(r, userCounts, formCounts)).ToList();
    }

    public async Task<RoleDto?> GetByIdAsync(int roleId, CancellationToken cancellationToken = default)
    {
        var role = await _uow.Roles.GetByIdAsync(roleId, cancellationToken);
        if (role is null) return null;

        return ToDto(role,
            await _uow.Roles.GetUserCountsAsync(cancellationToken),
            await _uow.ModuleRoleMappings.GetCountsByRoleAsync(cancellationToken));
    }

    public async Task<int> CreateAsync(CreateRoleDto dto, CancellationToken cancellationToken = default)
    {
        await _createValidator.ValidateAndThrowAsync(dto, cancellationToken);

        var name = dto.RoleName.Trim();
        if (await _uow.Roles.NameExistsAsync(name, null, cancellationToken))
            throw new ConflictException($"A role named '{name}' already exists.");

        var entity = new Role
        {
            RoleName = name,
            Description = Clean(dto.Description)
        };

        await _uow.Roles.AddAsync(entity, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);
        return entity.RoleId;
    }

    public async Task UpdateAsync(int roleId, UpdateRoleDto dto, CancellationToken cancellationToken = default)
    {
        await _updateValidator.ValidateAndThrowAsync(dto, cancellationToken);

        var entity = await _uow.Roles.GetByIdAsync(roleId, cancellationToken)
            ?? throw new NotFoundException($"Role '{roleId}' was not found.");

        var name = dto.RoleName.Trim();

        // Authorisation policies and the seeded access rows are keyed to these names — only the description may change.
        if (IsSystemRole(entity.RoleName) && !string.Equals(entity.RoleName, name, StringComparison.Ordinal))
            throw new BadRequestException($"The built-in '{entity.RoleName}' role cannot be renamed.");

        if (await _uow.Roles.NameExistsAsync(name, roleId, cancellationToken))
            throw new ConflictException($"A role named '{name}' already exists.");

        entity.RoleName = name;
        entity.Description = Clean(dto.Description);

        _uow.Roles.Update(entity);
        await _uow.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(int roleId, CancellationToken cancellationToken = default)
    {
        var entity = await _uow.Roles.GetByIdAsync(roleId, cancellationToken)
            ?? throw new NotFoundException($"Role '{roleId}' was not found.");

        if (IsSystemRole(entity.RoleName))
            throw new BadRequestException($"The built-in '{entity.RoleName}' role cannot be deleted.");

        var userCounts = await _uow.Roles.GetUserCountsAsync(cancellationToken);
        if (userCounts.TryGetValue(roleId, out var users) && users > 0)
            throw new ConflictException(
                $"'{entity.RoleName}' is assigned to {users} {(users == 1 ? "user" : "users")}. Reassign them before deleting the role.");

        // The Role → ModuleRoleMappings FK is Restrict, so its access rows must go first.
        foreach (var mapping in await _uow.ModuleRoleMappings.GetByRoleTrackedAsync(roleId, cancellationToken))
            _uow.ModuleRoleMappings.Remove(mapping);

        _uow.Roles.Remove(entity);
        await _uow.SaveChangesAsync(cancellationToken);
    }

    private static RoleDto ToDto(Role r, Dictionary<int, int> userCounts, Dictionary<int, int> formCounts)
    {
        var users = userCounts.GetValueOrDefault(r.RoleId);
        var isSystem = IsSystemRole(r.RoleName);
        return new RoleDto
        {
            RoleId = r.RoleId,
            RoleName = r.RoleName,
            Description = r.Description,
            UserCount = users,
            FormCount = formCounts.GetValueOrDefault(r.RoleId),
            IsSystemRole = isSystem,
            CanDelete = !isSystem && users == 0,
            CreatedDate = r.CreatedDate,
            ModifiedDate = r.ModifiedDate
        };
    }

    private static bool IsSystemRole(string roleName)
        => string.Equals(roleName, RoleNames.Admin, StringComparison.OrdinalIgnoreCase)
        || string.Equals(roleName, RoleNames.Sanathan, StringComparison.OrdinalIgnoreCase);

    private static string? Clean(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
}
