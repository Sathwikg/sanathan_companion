using Sanathana.Companion.Application.DTOs.Pujas;

namespace Sanathana.Companion.Application.Interfaces;

public interface IPujaProcessService
{
    // ---- admin configuration ----
    Task<PujaProcessConfigDto> GetConfigAsync(Guid pujaId, CancellationToken cancellationToken = default);
    Task SaveConfigAsync(Guid pujaId, SavePujaProcessDto dto, CancellationToken cancellationToken = default);

    // ---- user runtime ----
    /// <summary>Festivals available as a filter — only those with a configured puja, current flagged.</summary>
    Task<IReadOnlyList<ProcessFestivalDto>> GetFestivalsAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Every puja with a configured process. <paramref name="festivalId"/> is a filter, not a
    /// requirement: null returns all of them, including pujas not linked to any festival.
    /// </summary>
    Task<IReadOnlyList<ProcessPujaSummaryDto>> GetPujasAsync(Guid userId, Guid? festivalId, CancellationToken cancellationToken = default);

    /// <summary>The process resolved into <paramref name="languageCode"/>.</summary>
    Task<PujaProcessViewDto?> GetProcessAsync(Guid pujaId, string? languageCode, CancellationToken cancellationToken = default);
}
