using Sanathana.Companion.Domain.Common;

namespace Sanathana.Companion.Domain.Entities;

/// <summary>A piece of feedback submitted by a user against a chosen issue type.
/// Audit columns (CreatedBy/CreatedDate/…) come from <see cref="BaseEntity"/>.</summary>
public class Feedback : BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid IssueTypeId { get; set; }
    public IssueType? IssueType { get; set; }

    /// <summary>The user who submitted the feedback.</summary>
    public Guid UserId { get; set; }

    public string Description { get; set; } = string.Empty;

    /// <summary>Triage status: New, Reviewed or Resolved.</summary>
    public string Status { get; set; } = "New";
}
