using Sanathana.Companion.Application.DTOs.Feedback;

namespace Sanathana.Companion.Application.Interfaces;

public interface IFeedbackService
{
    /// <summary>Saves a feedback submitted by the given user. Returns the new feedback id.</summary>
    Task<Guid> SubmitAsync(Guid userId, SubmitFeedbackDto dto, CancellationToken cancellationToken = default);

    /// <summary>All feedback, newest first (admin).</summary>
    Task<IReadOnlyList<FeedbackDto>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Aggregated dashboard view (admin).</summary>
    Task<FeedbackDashboardDto> GetDashboardAsync(CancellationToken cancellationToken = default);

    Task UpdateStatusAsync(Guid id, string status, CancellationToken cancellationToken = default);
}
