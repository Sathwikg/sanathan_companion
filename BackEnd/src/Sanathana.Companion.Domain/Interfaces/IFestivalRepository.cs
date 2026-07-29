using Sanathana.Companion.Domain.Entities;

namespace Sanathana.Companion.Domain.Interfaces;

public interface IFestivalRepository : IRepository<Festival>
{
    Task<IReadOnlyList<Festival>> GetByYearAsync(int year, CancellationToken cancellationToken = default);

    /// <summary>Distinct years that have festivals, newest first (for the filter dropdown).</summary>
    Task<IReadOnlyList<int>> GetYearsAsync(CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(string name, int year, Guid? excludeId = null, CancellationToken cancellationToken = default);

    /// <summary>Distinct names of active festivals (for the deity festival multi-select).</summary>
    Task<IReadOnlyList<string>> GetActiveNamesAsync(CancellationToken cancellationToken = default);
}
