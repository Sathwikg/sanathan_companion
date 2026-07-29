using Microsoft.EntityFrameworkCore;
using Sanathana.Companion.Domain.Entities;
using Sanathana.Companion.Domain.Interfaces;

namespace Sanathana.Companion.Infrastructure.Persistence.Repositories;

public class FestivalRepository : BaseRepository<Festival>, IFestivalRepository
{
    public FestivalRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IReadOnlyList<Festival>> GetByYearAsync(int year, CancellationToken cancellationToken = default)
        => await Set.AsNoTracking()
                    .Where(f => f.Year == year)
                    .OrderBy(f => f.Date)
                    .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<int>> GetYearsAsync(CancellationToken cancellationToken = default)
        => await Set.AsNoTracking()
                    .Select(f => f.Year)
                    .Distinct()
                    .OrderByDescending(y => y)
                    .ToListAsync(cancellationToken);

    public async Task<bool> ExistsAsync(string name, int year, Guid? excludeId = null, CancellationToken cancellationToken = default)
        => await Set.AnyAsync(f => f.Name == name && f.Year == year && (excludeId == null || f.Id != excludeId), cancellationToken);

    public async Task<IReadOnlyList<string>> GetActiveNamesAsync(CancellationToken cancellationToken = default)
        => await Set.AsNoTracking()
                    .Where(f => f.IsActive)
                    .Select(f => f.Name)
                    .Distinct()
                    .OrderBy(n => n)
                    .ToListAsync(cancellationToken);
}
