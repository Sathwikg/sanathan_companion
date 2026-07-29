using Sanathana.Companion.Application.DTOs.Regions;

namespace Sanathana.Companion.Application.Interfaces;

public interface IRegionService
{
    Task<IReadOnlyList<RegionDto>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Active regions as id + name only — safe for the anonymous registration form.</summary>
    Task<IReadOnlyList<RegionOptionDto>> GetOptionsAsync(CancellationToken cancellationToken = default);
    Task<RegionDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Guid> CreateAsync(CreateRegionDto dto, CancellationToken cancellationToken = default);
    Task UpdateAsync(Guid id, UpdateRegionDto dto, CancellationToken cancellationToken = default);
    Task SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken = default);
}
