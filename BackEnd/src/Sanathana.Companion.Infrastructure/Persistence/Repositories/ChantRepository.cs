using Microsoft.EntityFrameworkCore;
using Sanathana.Companion.Domain.Entities;
using Sanathana.Companion.Domain.Interfaces;

namespace Sanathana.Companion.Infrastructure.Persistence.Repositories;

public class ChantRepository : BaseRepository<Chant>, IChantRepository
{
    public ChantRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IReadOnlyList<Chant>> GetAllOrderedAsync(CancellationToken cancellationToken = default)
        => await Set.AsNoTracking().OrderBy(c => c.Name).ToListAsync(cancellationToken);

    public async Task<bool> NameExistsAsync(string name, Guid? excludeId = null, CancellationToken cancellationToken = default)
        => await Set.AnyAsync(c => c.Name == name && (excludeId == null || c.Id != excludeId), cancellationToken);
}
