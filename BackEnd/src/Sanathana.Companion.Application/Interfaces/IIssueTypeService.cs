using Sanathana.Companion.Application.DTOs.IssueTypes;

namespace Sanathana.Companion.Application.Interfaces;

public interface IIssueTypeService
{
    /// <summary>All issue types (management list).</summary>
    Task<IReadOnlyList<IssueTypeDto>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Only active issue types, for the feedback form dropdown.</summary>
    Task<IReadOnlyList<IssueTypeDto>> GetActiveAsync(CancellationToken cancellationToken = default);

    Task<IssueTypeDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Guid> CreateAsync(CreateIssueTypeDto dto, CancellationToken cancellationToken = default);
    Task UpdateAsync(Guid id, UpdateIssueTypeDto dto, CancellationToken cancellationToken = default);
    Task SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken = default);
}
