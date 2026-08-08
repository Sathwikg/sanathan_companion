using Microsoft.EntityFrameworkCore;
using Sanathana.Companion.Domain.Entities;
using Sanathana.Companion.Domain.Interfaces;

namespace Sanathana.Companion.Infrastructure.Persistence.Repositories;

public class PujaRepository : BaseRepository<Puja>, IPujaRepository
{
    public PujaRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IReadOnlyList<Puja>> GetAllWithFestivalAsync(CancellationToken cancellationToken = default)
        => await Set.AsNoTracking()
            .Include(p => p.Festival)
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken);

    public async Task<bool> NameExistsAsync(
        string name, Guid festivalId, Guid? excludeId = null, CancellationToken cancellationToken = default)
        => await Set.AnyAsync(
            p => p.Name == name
                 && p.FestivalId == festivalId
                 && (excludeId == null || p.Id != excludeId),
            cancellationToken);
}
