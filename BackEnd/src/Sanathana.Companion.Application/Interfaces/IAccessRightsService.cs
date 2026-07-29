using Sanathana.Companion.Application.DTOs.AccessRights;

namespace Sanathana.Companion.Application.Interfaces;

public interface IAccessRightsService
{
    /// <summary>Roles that access rights can be configured for (excludes Admin).</summary>
    Task<IReadOnlyList<AccessRoleDto>> GetAssignableRolesAsync(CancellationToken cancellationToken = default);

    /// <summary>Every form plus the selected role's current web/mobile access.</summary>
    Task<AccessMatrixDto> GetMatrixAsync(int roleId, CancellationToken cancellationToken = default);

    /// <summary>Persists the role's access matrix (upserts enabled rows, removes fully-disabled ones).</summary>
    Task SaveMatrixAsync(int roleId, SaveAccessRightsDto dto, CancellationToken cancellationToken = default);
}
