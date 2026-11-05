using Sanathana.Companion.Application.DTOs.Calendar;

namespace Sanathana.Companion.Application.Interfaces;

public interface ICalendarService
{
    /// <summary>
    /// The month for a region: every day's tithi, moon, observances, festivals, weekday deities and
    /// reading, plus the caller's own marks when <paramref name="userId"/> is given.
    /// </summary>
    Task<CalendarMonthDto> GetMonthAsync(int year, int month, Guid? regionId, Guid? userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CalendarEventDto>> GetEventsAsync(Guid userId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default);

    Task<CalendarEventDto> CreateEventAsync(Guid userId, SaveCalendarEventDto dto, CancellationToken cancellationToken = default);

    Task<CalendarEventDto> UpdateEventAsync(Guid userId, Guid id, SaveCalendarEventDto dto, CancellationToken cancellationToken = default);

    Task DeleteEventAsync(Guid userId, Guid id, CancellationToken cancellationToken = default);
}
