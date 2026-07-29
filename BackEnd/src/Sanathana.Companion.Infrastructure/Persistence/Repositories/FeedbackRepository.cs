using Microsoft.EntityFrameworkCore;
using Sanathana.Companion.Domain.Entities;
using Sanathana.Companion.Domain.Interfaces;

namespace Sanathana.Companion.Infrastructure.Persistence.Repositories;

public class FeedbackRepository : BaseRepository<Feedback>, IFeedbackRepository
{
    public FeedbackRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IReadOnlyList<Feedback>> GetAllWithTypeAsync(CancellationToken cancellationToken = default)
        => await Set.AsNoTracking()
                    .Include(f => f.IssueType)
                    .OrderByDescending(f => f.CreatedDate)
                    .ToListAsync(cancellationToken);
}
