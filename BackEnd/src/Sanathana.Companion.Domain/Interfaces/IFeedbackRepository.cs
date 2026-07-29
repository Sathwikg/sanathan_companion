using Sanathana.Companion.Domain.Entities;

namespace Sanathana.Companion.Domain.Interfaces;

public interface IFeedbackRepository : IRepository<Feedback>
{
    /// <summary>All feedback with its issue type, newest first.</summary>
    Task<IReadOnlyList<Feedback>> GetAllWithTypeAsync(CancellationToken cancellationToken = default);
}
