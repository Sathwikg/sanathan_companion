using Sanathana.Companion.Domain.Entities;

namespace Sanathana.Companion.Domain.Interfaces;

public interface IPujaRepository : IRepository<Puja>
{
    /// <summary>All pujas with their festival loaded, ordered by name — one query, not N+1.</summary>
    Task<IReadOnlyList<Puja>> GetAllWithFestivalAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// A puja name must be unique within its festival, not globally: the same rite can legitimately
    /// be listed against two different festivals.
    /// </summary>
    Task<bool> NameExistsAsync(string name, Guid festivalId, Guid? excludeId = null, CancellationToken cancellationToken = default);
}
