using Sanathana.Companion.Domain.Entities;

namespace Sanathana.Companion.Domain.Interfaces;

public interface IDayRepository : IRepository<Day>
{
    Task<IReadOnlyList<Day>> GetAllOrderedAsync(CancellationToken cancellationToken = default);
}
