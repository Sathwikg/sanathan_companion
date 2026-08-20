using Sanathana.Companion.Domain.Entities;

namespace Sanathana.Companion.Domain.Interfaces;

public interface IRefreshTokenRepository : IRepository<RefreshToken>
{
    /// <summary>
    /// The row for a presented token, with its user and that user's role.
    /// </summary>
    /// <remarks>
    /// The Role include is load-bearing, not incidental: the access token minted from this row
    /// reads <c>user.Role?.RoleName</c>, so without it every refreshed token would carry an empty
    /// role claim and quietly demote an administrator off their own screens.
    /// </remarks>
    Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken = default);

    /// <summary>Revokes every live token descended from one sign-in.</summary>
    Task RevokeFamilyAsync(Guid familyId, string reason, DateTime nowUtc, CancellationToken cancellationToken = default);

    /// <summary>Revokes every live token a user holds, across all their devices.</summary>
    Task RevokeAllForUserAsync(Guid userId, string reason, DateTime nowUtc, CancellationToken cancellationToken = default);
}
