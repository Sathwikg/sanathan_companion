using Sanathana.Companion.Domain.Entities;

namespace Sanathana.Companion.Domain.Interfaces;

public interface IMenuModuleRepository : IRepository<MenuModule>
{
    /// <summary>All menu items ordered by DisplayOrder then Name (untracked).</summary>
    Task<IReadOnlyList<MenuModule>> GetAllOrderedAsync(CancellationToken cancellationToken = default);

    Task<bool> HasChildrenAsync(Guid id, CancellationToken cancellationToken = default);
}
