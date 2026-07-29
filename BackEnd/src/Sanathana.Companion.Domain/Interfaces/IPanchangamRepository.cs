using Sanathana.Companion.Domain.Entities;

namespace Sanathana.Companion.Domain.Interfaces;

public interface IPanchangamRepository : IRepository<Panchangam>
{
    Task<IReadOnlyList<Panchangam>> GetFilteredAsync(
        int? year,
        Guid? regionId,
        DateOnly? from,
        DateOnly? to,
        string? search,
        CancellationToken cancellationToken = default);

    Task<Panchangam?> GetByDateAsync(DateOnly date, Guid regionId, CancellationToken cancellationToken = default);

    /// <summary>Distinct years that have stored data, newest first — computed in SQL.</summary>
    Task<IReadOnlyList<int>> GetYearsAsync(CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(DateOnly date, Guid regionId, Guid? excludeId, CancellationToken cancellationToken = default);

    /// <summary>Dates already stored for a region within a range — lets generation skip existing rows.</summary>
    Task<HashSet<DateOnly>> GetExistingDatesAsync(Guid regionId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default);

    Task AddRangeAsync(IEnumerable<Panchangam> items, CancellationToken cancellationToken = default);
}
