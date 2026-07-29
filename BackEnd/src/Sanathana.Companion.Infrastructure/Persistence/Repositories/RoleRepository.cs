using Microsoft.EntityFrameworkCore;
using Sanathana.Companion.Domain.Entities;
using Sanathana.Companion.Domain.Interfaces;

namespace Sanathana.Companion.Infrastructure.Persistence.Repositories;

public class RoleRepository : BaseRepository<Role>, IRoleRepository
{
    public RoleRepository(ApplicationDbContext context) : base(context) { }

    public async Task<Role?> GetByNameAsync(string roleName, CancellationToken cancellationToken = default)
        => await Set.FirstOrDefaultAsync(r => r.RoleName == roleName, cancellationToken);

    public async Task<IReadOnlyList<Role>> GetAllOrderedAsync(CancellationToken cancellationToken = default)
        => await Set.AsNoTracking().OrderBy(r => r.RoleName).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Role>> GetFilteredAsync(string? search, CancellationToken cancellationToken = default)
    {
        var query = Set.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = SqlLike.Contains(search);
            query = query.Where(r =>
                EF.Functions.ILike(r.RoleName, term, SqlLike.EscapeChar) ||
                (r.Description != null && EF.Functions.ILike(r.Description, term, SqlLike.EscapeChar)));
        }

        return await query.OrderBy(r => r.RoleName).ToListAsync(cancellationToken);
    }

    public async Task<bool> NameExistsAsync(string roleName, int? excludeId = null, CancellationToken cancellationToken = default)
        => await Set.AnyAsync(r => r.RoleName.ToLower() == roleName.ToLower()
            && (excludeId == null || r.RoleId != excludeId), cancellationToken);

    public async Task<Dictionary<int, int>> GetUserCountsAsync(CancellationToken cancellationToken = default)
        => await Context.Users
            .AsNoTracking()
            .GroupBy(u => u.RoleId)
            .Select(g => new { RoleId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.RoleId, x => x.Count, cancellationToken);
}
