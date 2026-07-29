using Sanathana.Companion.Application.DTOs.Deities;

namespace Sanathana.Companion.Application.Interfaces;

public interface IDeityService
{
    Task<IReadOnlyList<DeityDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<DeityDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<(byte[]? Data, string? ContentType)> GetImageAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Guid> CreateAsync(CreateDeityDto dto, CancellationToken cancellationToken = default);
    Task UpdateAsync(Guid id, UpdateDeityDto dto, CancellationToken cancellationToken = default);
    Task SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken = default);

    /// <summary>Region names, festival names, and day names for the form's multi-selects.</summary>
    Task<DeityFormOptionsDto> GetFormOptionsAsync(CancellationToken cancellationToken = default);
}
