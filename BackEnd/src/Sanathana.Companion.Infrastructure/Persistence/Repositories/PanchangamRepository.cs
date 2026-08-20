using Microsoft.EntityFrameworkCore;
using Sanathana.Companion.Domain.Entities;
using Sanathana.Companion.Domain.Interfaces;

namespace Sanathana.Companion.Infrastructure.Persistence.Repositories;

public class PanchangamRepository : BaseRepository<Panchangam>, IPanchangamRepository
{
    public PanchangamRepository(ApplicationDbContext context) : base(context) { }

    public async Task<(IReadOnlyList<Panchangam> Rows, int TotalCount)> GetPagedAsync(
        int? year,
        Guid? regionId,
        DateOnly? from,
        DateOnly? to,
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        // No Include on the base query: it is also what the count runs over, and a COUNT has no
        // business joining Regions.
        var query = Set.AsNoTracking().AsQueryable();

        if (year is not null) query = query.Where(p => p.Year == year);
        if (regionId is not null) query = query.Where(p => p.RegionId == regionId);
        if (from is not null) query = query.Where(p => p.Date >= from);
        if (to is not null) query = query.Where(p => p.Date <= to);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = SqlLike.Contains(search);
            query = query.Where(p =>
                (p.Masam != null && EF.Functions.ILike(p.Masam, term, SqlLike.EscapeChar)) ||
                (p.TithiDetails != null && EF.Functions.ILike(p.TithiDetails, term, SqlLike.EscapeChar)) ||
                (p.NakshatramDetails != null && EF.Functions.ILike(p.NakshatramDetails, term, SqlLike.EscapeChar)) ||
                (p.TeluguSamvatsaram != null && EF.Functions.ILike(p.TeluguSamvatsaram, term, SqlLike.EscapeChar)));
        }

        var total = await query.CountAsync(cancellationToken);

        var rows = await query
            .Include(p => p.Region)
            .OrderBy(p => p.Date).ThenBy(p => p.Region!.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (rows, total);
    }

    public async Task<Panchangam?> GetByDateAsync(DateOnly date, Guid regionId, CancellationToken cancellationToken = default)
        => await Set.AsNoTracking().Include(p => p.Region)
            .FirstOrDefaultAsync(p => p.Date == date && p.RegionId == regionId, cancellationToken);

    public async Task<IReadOnlyList<int>> GetYearsAsync(CancellationToken cancellationToken = default)
        => await Set.AsNoTracking().Select(p => p.Year).Distinct()
            .OrderByDescending(y => y).ToListAsync(cancellationToken);

    public async Task<bool> ExistsAsync(DateOnly date, Guid regionId, Guid? excludeId, CancellationToken cancellationToken = default)
        => await Set.AnyAsync(p => p.Date == date && p.RegionId == regionId && (excludeId == null || p.Id != excludeId), cancellationToken);

    public async Task<HashSet<DateOnly>> GetExistingDatesAsync(Guid regionId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        var dates = await Set.AsNoTracking()
            .Where(p => p.RegionId == regionId && p.Date >= from && p.Date <= to)
            .Select(p => p.Date)
            .ToListAsync(cancellationToken);
        return dates.ToHashSet();
    }

    public async Task AddRangeAsync(IEnumerable<Panchangam> items, CancellationToken cancellationToken = default)
        => await Set.AddRangeAsync(items, cancellationToken);
}
