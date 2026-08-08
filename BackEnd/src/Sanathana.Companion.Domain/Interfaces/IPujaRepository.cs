using Sanathana.Companion.Domain.Entities;

namespace Sanathana.Companion.Domain.Interfaces;

public interface IPujaRepository : IRepository<Puja>
{
    /// <summary>All pujas with festival and deity loaded, ordered by name — one query, not N+1.</summary>
    Task<IReadOnlyList<Puja>> GetAllWithLinksAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// A puja name must be unique within its festival, not globally: the same rite can legitimately
    /// be listed against two different festivals. <paramref name="festivalId"/> may be null, which
    /// scopes the check to the pujas that are not mapped to any festival.
    /// </summary>
    Task<bool> NameExistsAsync(string name, Guid? festivalId, Guid? excludeId = null, CancellationToken cancellationToken = default);
}
