using Sanathana.Companion.Domain.Entities;

namespace Sanathana.Companion.Domain.Interfaces;

public interface IRoleRepository : IRepository<Role>
{
    Task<Role?> GetByNameAsync(string roleName, CancellationToken cancellationToken = default);

    /// <summary>All roles ordered by name (for the Role master).</summary>
    Task<IReadOnlyList<Role>> GetAllOrderedAsync(CancellationToken cancellationToken = default);

    /// <summary>Roles matched by free text on name or description, ordered by name.</summary>
    Task<IReadOnlyList<Role>> GetFilteredAsync(string? search, CancellationToken cancellationToken = default);

    /// <summary>True when another role already carries this name (case-insensitive).</summary>
    Task<bool> NameExistsAsync(string roleName, int? excludeId = null, CancellationToken cancellationToken = default);

    /// <summary>How many users are assigned to each role, keyed by role id. Roles with no users are omitted.</summary>
    Task<Dictionary<int, int>> GetUserCountsAsync(CancellationToken cancellationToken = default);
}
