using Microsoft.EntityFrameworkCore;
using Sanathana.Companion.Domain.Entities;
using Sanathana.Companion.Domain.Interfaces;

namespace Sanathana.Companion.Infrastructure.Persistence.Repositories;

public class DayRepository : BaseRepository<Day>, IDayRepository
{
    public DayRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IReadOnlyList<Day>> GetAllOrderedAsync(CancellationToken cancellationToken = default)
        => await Set.AsNoTracking().OrderBy(d => d.DisplayOrder).ToListAsync(cancellationToken);
}
