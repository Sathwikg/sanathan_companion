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
}
