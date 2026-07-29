using Microsoft.EntityFrameworkCore;
using Sanathana.Companion.Domain.Entities;
using Sanathana.Companion.Domain.Interfaces;

namespace Sanathana.Companion.Infrastructure.Persistence.Repositories;

public class MenuModuleRepository : BaseRepository<MenuModule>, IMenuModuleRepository
{
    public MenuModuleRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IReadOnlyList<MenuModule>> GetAllOrderedAsync(CancellationToken cancellationToken = default)
        => await Set.AsNoTracking()
                    .OrderBy(m => m.DisplayOrder)
                    .ThenBy(m => m.Name)
                    .ToListAsync(cancellationToken);

    public async Task<bool> HasChildrenAsync(Guid id, CancellationToken cancellationToken = default)
        => await Set.AnyAsync(m => m.ParentId == id, cancellationToken);
}
