using Sanathana.Companion.Domain.Entities;

namespace Sanathana.Companion.Domain.Interfaces;

public interface IIssueTypeRepository : IRepository<IssueType>
{
    /// <summary>All issue types, ordered for display.</summary>
    Task<IReadOnlyList<IssueType>> GetAllOrderedAsync(CancellationToken cancellationToken = default);

    Task<bool> NameExistsAsync(string name, Guid? excludeId = null, CancellationToken cancellationToken = default);
}
