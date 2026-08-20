using Sanathana.Companion.Application.DTOs.Users;

namespace Sanathana.Companion.Application.Interfaces;

public interface IUserService
{
    Task<IReadOnlyList<UserListItemDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<UserProfileDto?> GetProfileAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>The signed-in user's own profile with a per-day sadhana timeline.</summary>
    Task<MyProfileDto?> GetMyProfileAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Sets (or clears, when null) the user's preferred region.</summary>
    Task UpdateDefaultRegionAsync(Guid userId, Guid? regionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens or closes an account. Closing also ends every live session that account holds.
    /// </summary>
    /// <param name="actingUserId">
    /// The administrator doing it, passed in rather than resolved here so the service keeps its
    /// three dependencies. Used to refuse closing your own account.
    /// </param>
    Task SetActiveAsync(Guid userId, bool isActive, Guid actingUserId, CancellationToken cancellationToken = default);

    /// <summary>Everything the app holds about this user, for them to take away.</summary>
    Task<MyDataExportDto?> ExportMyDataAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Permanently deletes the user and everything belonging to them. Returns false when the
    /// supplied password does not match, so the caller can answer 400 rather than 401.
    /// </summary>
    Task<bool> DeleteMyAccountAsync(Guid userId, string password, CancellationToken cancellationToken = default);
}
