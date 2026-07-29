using Sanathana.Companion.Domain.Entities;

namespace Sanathana.Companion.Domain.Interfaces;

public interface IUserRepository : IRepository<User>
{
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>Finds a user whose email OR mobile number matches the credential, including the Role navigation.</summary>
    Task<User?> GetByEmailOrMobileAsync(string credential, CancellationToken cancellationToken = default);

    Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>All users with their role, newest registrations first (for the User master).</summary>
    Task<IReadOnlyList<User>> GetAllWithRolesAsync(CancellationToken cancellationToken = default);

    /// <summary>A single user with their role.</summary>
    Task<User?> GetWithRoleAsync(Guid userId, CancellationToken cancellationToken = default);
}
