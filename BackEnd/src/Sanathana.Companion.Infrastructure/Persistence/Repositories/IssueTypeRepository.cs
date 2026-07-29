using Microsoft.EntityFrameworkCore;
using Sanathana.Companion.Domain.Entities;
using Sanathana.Companion.Domain.Interfaces;

namespace Sanathana.Companion.Infrastructure.Persistence.Repositories;

public class IssueTypeRepository : BaseRepository<IssueType>, IIssueTypeRepository
{
    public IssueTypeRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IReadOnlyList<IssueType>> GetAllOrderedAsync(CancellationToken cancellationToken = default)
        => await Set.AsNoTracking()
                    .OrderBy(t => t.DisplayOrder).ThenBy(t => t.Name)
                    .ToListAsync(cancellationToken);

    public async Task<bool> NameExistsAsync(string name, Guid? excludeId = null, CancellationToken cancellationToken = default)
        => await Set.AnyAsync(t => t.Name == name && (excludeId == null || t.Id != excludeId), cancellationToken);
}
