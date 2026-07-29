using Sanathana.Companion.Application.DTOs.Panchangams;

namespace Sanathana.Companion.Application.Interfaces;

public interface IPanchangamService
{
    Task<IReadOnlyList<PanchangamDto>> GetAllAsync(
        int? year, Guid? regionId, DateOnly? from, DateOnly? to, string? search,
        CancellationToken cancellationToken = default);

    Task<PanchangamDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>The stored row for a date + region, or null.</summary>
    Task<PanchangamDto?> GetByDateAsync(DateOnly date, Guid regionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Compute a day's Panchangam for arbitrary coordinates WITHOUT storing it — the path
    /// used for the user's current geolocation. This is the same generic engine that seeds
    /// the stored rows, so a computed result is identical to what would be stored.
    /// </summary>
    Task<PanchangamDto> ComputeAtLocationAsync(DateOnly date, double latitude, double longitude, string? placeLabel = null, CancellationToken cancellationToken = default);

    /// <summary>Years for which any stored data exists.</summary>
    Task<IReadOnlyList<int>> GetStoredYearsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PanchangamRegionOptionDto>> GetRegionOptionsAsync(CancellationToken cancellationToken = default);

    /// <summary>Bulk-generate and store an entire year for one region (or all with coordinates).</summary>
    Task<GenerateResultDto> GenerateAsync(GeneratePanchangamDto dto, CancellationToken cancellationToken = default);
}
