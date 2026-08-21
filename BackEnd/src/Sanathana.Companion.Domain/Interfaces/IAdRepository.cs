using Sanathana.Companion.Domain.Entities;

namespace Sanathana.Companion.Domain.Interfaces;

/// <summary>Ad formats, per-form placements, and the one settings row.</summary>
public interface IAdRepository
{
    Task<IReadOnlyList<AdFormat>> GetFormatsAsync(CancellationToken cancellationToken = default);

    /// <summary>Every placement with its module and format loaded.</summary>
    Task<IReadOnlyList<AdPlacement>> GetPlacementsAsync(CancellationToken cancellationToken = default);

    Task<AdPlacement?> GetPlacementForModuleAsync(Guid menuModuleId, CancellationToken cancellationToken = default);

    Task AddPlacementAsync(AdPlacement placement, CancellationToken cancellationToken = default);

    void RemovePlacement(AdPlacement placement);

    /// <summary>The single settings row, created on first read if the seed is missing.</summary>
    Task<AdSettings> GetSettingsAsync(CancellationToken cancellationToken = default);

    Task<AdFormat?> GetFormatByIdAsync(Guid id, CancellationToken cancellationToken = default);
}
