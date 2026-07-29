using Microsoft.EntityFrameworkCore;
using Sanathana.Companion.Domain.Entities;
using Sanathana.Companion.Domain.Interfaces;

namespace Sanathana.Companion.Infrastructure.Persistence.Repositories;

public class RegionRepository : BaseRepository<Region>, IRegionRepository
{
    public RegionRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IReadOnlyList<Region>> GetAllOrderedAsync(CancellationToken cancellationToken = default)
        => await Set.AsNoTracking()
                    .OrderBy(r => r.Name)
                    .ToListAsync(cancellationToken);

    public async Task<bool> NameExistsAsync(string name, Guid? excludeId = null, CancellationToken cancellationToken = default)
        => await Set.AnyAsync(r => r.Name == name && (excludeId == null || r.Id != excludeId), cancellationToken);
}
