using Sanathana.Companion.Domain.Entities;

namespace Sanathana.Companion.Domain.Interfaces;

public interface ICalendarEventRepository : IRepository<CalendarEvent>
{
    /// <summary>
    /// A seeker's marks that can fall inside a date range: every one-off dated within it, plus
    /// every yearly repeat whose first occurrence is not after its end. The caller expands the
    /// repeats onto the year in view.
    /// </summary>
    Task<IReadOnlyList<CalendarEvent>> GetForRangeAsync(Guid userId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default);

    /// <summary>The row, but only if it belongs to this seeker — anyone else's id is "not found".</summary>
    Task<CalendarEvent?> GetOwnedAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
}
