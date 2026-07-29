using Sanathana.Companion.Application.DTOs.Roles;

namespace Sanathana.Companion.Application.Interfaces;

public interface IRoleService
{
    Task<IReadOnlyList<RoleDto>> GetAllAsync(string? search, CancellationToken cancellationToken = default);
    Task<RoleDto?> GetByIdAsync(int roleId, CancellationToken cancellationToken = default);
    Task<int> CreateAsync(CreateRoleDto dto, CancellationToken cancellationToken = default);
    Task UpdateAsync(int roleId, UpdateRoleDto dto, CancellationToken cancellationToken = default);

    /// <summary>Deletes a role along with its access-rights rows. Blocked for system roles and roles still in use.</summary>
    Task DeleteAsync(int roleId, CancellationToken cancellationToken = default);
}
