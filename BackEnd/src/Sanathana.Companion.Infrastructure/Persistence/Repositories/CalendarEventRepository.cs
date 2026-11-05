using Microsoft.EntityFrameworkCore;
using Sanathana.Companion.Domain.Entities;
using Sanathana.Companion.Domain.Interfaces;

namespace Sanathana.Companion.Infrastructure.Persistence.Repositories;

public class CalendarEventRepository : BaseRepository<CalendarEvent>, ICalendarEventRepository
{
    public CalendarEventRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IReadOnlyList<CalendarEvent>> GetForRangeAsync(Guid userId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
        => await Set.AsNoTracking()
                    .Where(e => e.UserId == userId &&
                                ((e.Date >= from && e.Date <= to) || (e.RepeatYearly && e.Date <= to)))
                    .OrderBy(e => e.Date).ThenBy(e => e.Time)
                    .ToListAsync(cancellationToken);

    public async Task<CalendarEvent?> GetOwnedAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
        => await Set.FirstOrDefaultAsync(e => e.Id == id && e.UserId == userId, cancellationToken);
}
