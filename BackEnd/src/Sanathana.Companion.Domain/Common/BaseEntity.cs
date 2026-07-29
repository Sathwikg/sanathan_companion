namespace Sanathana.Companion.Domain.Common;

/// <summary>
/// Base type for all persisted entities. Carries the audit columns that are
/// auto-stamped by the DbContext on save. The primary key is defined by each
/// concrete entity (Role uses an int identity, User uses a Guid).
/// </summary>
public abstract class BaseEntity
{
    public string? CreatedBy { get; set; }
    public DateTime CreatedDate { get; set; }
    public string? ModifiedBy { get; set; }
    public DateTime? ModifiedDate { get; set; }
}
