using Sanathana.Companion.Application.DTOs.Sadhana;

namespace Sanathana.Companion.Application.Interfaces;

public interface ISadhanaService
{
    /// <summary>Today's recommended chants (day + deity, festival override), the user's sessions and streak.</summary>
    Task<SadhanaTodayDto> GetTodayAsync(Guid? regionId, CancellationToken cancellationToken = default);

    /// <summary>All active chants for the "search saved chants" tab, with today's progress.</summary>
    Task<IReadOnlyList<SadhanaChantDto>> GetChantsAsync(string? search, Guid? regionId = null, CancellationToken cancellationToken = default);

    /// <summary>Full chant detail plus the user's progress today.</summary>
    Task<SadhanaChantDetailDto?> GetChantAsync(Guid chantConfigId, CancellationToken cancellationToken = default);

    /// <summary>Record the japa count for a chant today; updates malas and the streak.</summary>
    Task<LogCountResultDto> LogCountAsync(LogCountDto dto, CancellationToken cancellationToken = default);

    Task<SadhanaStreakDto> GetStreakAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves the seeker's own mala size and vibration choice for a chant and returns the chant as
    /// the japa screen now shows it. The mala in progress keeps its count.
    /// </summary>
    Task<SadhanaChantDetailDto> SaveChantSettingsAsync(Guid chantConfigId, SaveChantSettingsDto dto, CancellationToken cancellationToken = default);
}
