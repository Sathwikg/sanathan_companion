using Sanathana.Companion.Domain.Entities;

namespace Sanathana.Companion.Domain.Interfaces;

public interface IPanchangamRepository : IRepository<Panchangam>
{
    /// <summary>
    /// One page of matching rows, ordered by date then region name, with the unpaged total.
    /// </summary>
    /// <remarks>
    /// The ordering is a total order — region names are unique — so paging cannot duplicate or
    /// skip a row.
    /// </remarks>
    Task<(IReadOnlyList<Panchangam> Rows, int TotalCount)> GetPagedAsync(
        int? year,
        Guid? regionId,
        DateOnly? from,
        DateOnly? to,
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<Panchangam?> GetByDateAsync(DateOnly date, Guid regionId, CancellationToken cancellationToken = default);

    /// <summary>Distinct years that have stored data, newest first — computed in SQL.</summary>
    Task<IReadOnlyList<int>> GetYearsAsync(CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(DateOnly date, Guid regionId, Guid? excludeId, CancellationToken cancellationToken = default);

    /// <summary>Dates already stored for a region within a range — lets generation skip existing rows.</summary>
    Task<HashSet<DateOnly>> GetExistingDatesAsync(Guid regionId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default);

    Task AddRangeAsync(IEnumerable<Panchangam> items, CancellationToken cancellationToken = default);
}
