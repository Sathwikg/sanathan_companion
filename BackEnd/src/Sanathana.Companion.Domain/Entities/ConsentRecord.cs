using Sanathana.Companion.Domain.Common;

namespace Sanathana.Companion.Domain.Entities;

/// <summary>
/// One consent given or withdrawn: which purpose, against which version of the privacy notice,
/// when, and from which app. Append-only: a change of mind is a new row, so the history answers
/// "did this seeker agree to this, and when" (DPDP Act 2023, section 6(10)).
/// </summary>
/// <remarks>
/// A plain user id, like <see cref="UserFavorite"/>. Removed with the rest of the seeker's data when
/// the account is deleted, and listed in their data export.
/// </remarks>
public class ConsentRecord : BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }

    /// <summary>One of the Application layer's ConsentPurposes.</summary>
    public string Purpose { get; set; } = string.Empty;

    public bool Granted { get; set; }

    /// <summary>The privacy notice version on screen when the choice was made.</summary>
    public int NoticeVersion { get; set; }

    public DateTime AtUtc { get; set; }

    /// <summary>"Android", "iOS" or "Web": where the choice was made.</summary>
    public string? Platform { get; set; }
}
