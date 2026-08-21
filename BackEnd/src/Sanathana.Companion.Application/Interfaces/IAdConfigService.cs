using Sanathana.Companion.Application.DTOs.Ads;

namespace Sanathana.Companion.Application.Interfaces;

public interface IAdConfigService
{
    /// <summary>The whole config screen: settings, the format master, and every navigable form.</summary>
    Task<AdConfigDto> GetConfigAsync(CancellationToken cancellationToken = default);

    Task SaveConfigAsync(SaveAdConfigDto dto, CancellationToken cancellationToken = default);

    /// <summary>
    /// What, if anything, the given form should show on the given platform.
    /// </summary>
    /// <param name="platform">"Android" or "iOS"; anything else resolves to no ad.</param>
    Task<AdSlotDto> GetSlotAsync(Guid menuModuleId, string? platform, CancellationToken cancellationToken = default);
}
