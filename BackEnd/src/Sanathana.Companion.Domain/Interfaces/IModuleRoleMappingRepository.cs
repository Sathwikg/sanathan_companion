using Sanathana.Companion.Domain.Entities;

namespace Sanathana.Companion.Domain.Interfaces;

public interface IModuleRoleMappingRepository : IRepository<ModuleRoleMapping>
{
    /// <summary>All access rows for a role (no tracking) — used to build the menu / matrix.</summary>
    Task<IReadOnlyList<ModuleRoleMapping>> GetByRoleAsync(int roleId, CancellationToken cancellationToken = default);

    /// <summary>All access rows for a role, tracked — used by the save/sync path.</summary>
    Task<List<ModuleRoleMapping>> GetByRoleTrackedAsync(int roleId, CancellationToken cancellationToken = default);

    /// <summary>How many forms each role has been granted, keyed by role id. Roles with no grants are omitted.</summary>
    Task<Dictionary<int, int>> GetCountsByRoleAsync(CancellationToken cancellationToken = default);
}
