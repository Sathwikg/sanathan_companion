using Microsoft.EntityFrameworkCore;
using Sanathana.Companion.Domain.Entities;
using Sanathana.Companion.Domain.Interfaces;

namespace Sanathana.Companion.Infrastructure.Persistence.Repositories;

public class ModuleRoleMappingRepository : BaseRepository<ModuleRoleMapping>, IModuleRoleMappingRepository
{
    public ModuleRoleMappingRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IReadOnlyList<ModuleRoleMapping>> GetByRoleAsync(int roleId, CancellationToken cancellationToken = default)
        => await Set.AsNoTracking()
            .Where(x => x.RoleId == roleId)
            .ToListAsync(cancellationToken);

    public async Task<List<ModuleRoleMapping>> GetByRoleTrackedAsync(int roleId, CancellationToken cancellationToken = default)
        => await Set.Where(x => x.RoleId == roleId).ToListAsync(cancellationToken);

    public async Task<Dictionary<int, int>> GetCountsByRoleAsync(CancellationToken cancellationToken = default)
        => await Set.AsNoTracking()
            .GroupBy(x => x.RoleId)
            .Select(g => new { RoleId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.RoleId, x => x.Count, cancellationToken);
}
