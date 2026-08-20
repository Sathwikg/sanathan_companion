using Sanathana.Companion.Application.DTOs.Deities;

namespace Sanathana.Companion.Application.Interfaces;

public interface IDeityService
{
    Task<IReadOnlyList<DeityDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<DeityDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    /// <summary>The stored bytes, or nulls when the row is missing or not published.</summary>
    Task<(byte[]? Data, string? ContentType)> GetImageAsync(Guid id, bool includeInactive = false, CancellationToken cancellationToken = default);
    Task<Guid> CreateAsync(CreateDeityDto dto, CancellationToken cancellationToken = default);
    Task UpdateAsync(Guid id, UpdateDeityDto dto, CancellationToken cancellationToken = default);
    Task SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken = default);

    /// <summary>Region names, festival names, and day names for the form's multi-selects.</summary>
    Task<DeityFormOptionsDto> GetFormOptionsAsync(CancellationToken cancellationToken = default);
}
