using Microsoft.EntityFrameworkCore;
using Sanathana.Companion.Domain.Entities;
using Sanathana.Companion.Domain.Interfaces;

namespace Sanathana.Companion.Infrastructure.Persistence.Repositories;

public class PujaRepository : BaseRepository<Puja>, IPujaRepository
{
    public PujaRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IReadOnlyList<Puja>> GetAllWithLinksAsync(CancellationToken cancellationToken = default)
        => await Set.AsNoTracking()
            .Include(p => p.Festival)
            // Deity carries an image blob, so pull only the two fields the list renders rather
            // than Include()-ing the whole row.
            .Select(p => new Puja
            {
                Id = p.Id,
                Name = p.Name,
                Description = p.Description,
                FestivalId = p.FestivalId,
                Festival = p.Festival,
                DeityId = p.DeityId,
                Deity = p.DeityId == null ? null : new Deity { Id = p.Deity!.Id, Name = p.Deity.Name },
                IsActive = p.IsActive
            })
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken);

    public async Task<bool> NameExistsAsync(
        string name, Guid? festivalId, Guid? excludeId = null, CancellationToken cancellationToken = default)
        // EF Core's null semantics turn `p.FestivalId == festivalId` into an IS NULL comparison
        // when the parameter is null, so unmapped pujas are compared against each other correctly
        // rather than silently never matching.
        => await Set.AnyAsync(
            p => p.Name == name
                 && p.FestivalId == festivalId
                 && (excludeId == null || p.Id != excludeId),
            cancellationToken);
}
