using Sanathana.Companion.Domain.Entities;

namespace Sanathana.Companion.Domain.Interfaces;

public interface IUserRepository : IRepository<User>
{
    /// <summary>
    /// Finds a user whose email OR mobile number matches the credential, including the Role navigation.
    /// </summary>
    /// <remarks>
    /// The argument is what the seeker typed. Implementations normalise it with
    /// <see cref="Domain.Common.CredentialNormalizer"/> before comparing, because that is the
    /// spelling the columns hold.
    /// </remarks>
    Task<User?> GetByEmailOrMobileAsync(string credential, CancellationToken cancellationToken = default);

    /// <summary>True when an account already holds this email. Normalised by the implementation.</summary>
    Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>True when an account already holds this mobile number. Normalised by the implementation.</summary>
    Task<bool> MobileExistsAsync(string mobile, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes the user and everything belonging to them: sadhana log and streak, favourites,
    /// notification settings and preferences, and their feedback.
    /// </summary>
    /// <remarks>
    /// Explicit rather than relying on cascade configuration — the FKs do not agree with each
    /// other (feedback is Restrict, so a cascade delete would simply fail for anyone who has ever
    /// sent any), and "what exactly gets erased" is not something to leave to a default.
    /// Does not save; the unit of work commits.
    /// </remarks>
    Task PurgeUserDataAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>All users with their role, newest registrations first (for the User master).</summary>
    Task<IReadOnlyList<User>> GetAllWithRolesAsync(CancellationToken cancellationToken = default);

    /// <summary>A single user with their role.</summary>
    Task<User?> GetWithRoleAsync(Guid userId, CancellationToken cancellationToken = default);
}
