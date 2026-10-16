using Sanathana.Companion.Domain.Entities;

namespace Sanathana.Companion.Domain.Interfaces;

public interface IDeviceTokenRepository : IRepository<DeviceToken>
{
    Task<IReadOnlyList<DeviceToken>> GetActiveByUserAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DeviceToken>> GetActiveByPlatformAsync(int platform, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DeviceToken>> GetActiveByModuleAsync(Guid menuModuleId, int? platform = null, CancellationToken cancellationToken = default);

    Task<DeviceToken?> GetByTokenAsync(string token, CancellationToken cancellationToken = default);

    Task<int> MarkInactiveAsync(string token, string reason, CancellationToken cancellationToken = default);

    Task<int> IncrementFailureCountAsync(string token, string reason, CancellationToken cancellationToken = default);

    Task<int> ResetFailureCountAsync(string token, CancellationToken cancellationToken = default);

    Task<int> UpdateLastUsedAsync(string token, DateTime usedAt, CancellationToken cancellationToken = default);

    Task<int> DeleteInactiveOlderThanAsync(DateTime cutoff, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DeviceToken>> GetAllAsync(CancellationToken cancellationToken = default);
}