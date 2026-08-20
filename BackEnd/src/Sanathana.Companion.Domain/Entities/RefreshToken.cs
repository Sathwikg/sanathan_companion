using Sanathana.Companion.Domain.Common;

namespace Sanathana.Companion.Domain.Entities;

/// <summary>
/// A long-lived credential that buys a new access token, so a seeker is not signed out mid-practice.
/// </summary>
/// <remarks>
/// Only the SHA-256 of the value is stored. A database dump is then a list of dead hashes rather
/// than a set of live sessions — the same reasoning as password hashing, for the same reason.
/// <para>
/// Every token descended from one sign-in shares a <see cref="FamilyId"/>. Rotation replaces a
/// token on each use; if a token that has already been rotated is presented again, either it was
/// stolen or the real device replayed it, and there is no way to tell which — so the whole family
/// is revoked and both parties have to sign in again.
/// </para>
/// </remarks>
public class RefreshToken : BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    /// <summary>Hex SHA-256 of the value handed to the client. Exactly 64 characters.</summary>
    public string TokenHash { get; set; } = string.Empty;

    /// <summary>Groups every token descended from one sign-in.</summary>
    public Guid FamilyId { get; set; }

    public DateTime ExpiresAtUtc { get; set; }

    public DateTime? RevokedAtUtc { get; set; }

    /// <summary>The successor minted when this one was used. Null unless it was rotated.</summary>
    public Guid? ReplacedByTokenId { get; set; }

    /// <summary>Why it was revoked: rotated, reuse-detected, signed-out, password-changed, disabled.</summary>
    public string? RevokedReason { get; set; }
}
