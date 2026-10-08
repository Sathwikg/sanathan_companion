namespace Sanathana.Companion.Domain.Entities;

/// <summary>
/// Records database data mutations (Insert, Update, Delete) with old and new values.
/// </summary>
public class AuditDataLog
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid? UserId { get; set; }
    public string? UsernameOrEmail { get; set; }

    /// <summary>INSERT, UPDATE, or DELETE</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>The entity/table name (e.g. "Deity", "Chant", "Festival")</summary>
    public string EntityName { get; set; } = string.Empty;

    /// <summary>Primary key value of the mutated entity</summary>
    public string? EntityId { get; set; }

    /// <summary>Associated ModuleCode if known</summary>
    public string? ModuleCode { get; set; }

    /// <summary>Comma-separated or JSON list of modified properties/columns</summary>
    public string? ChangedColumns { get; set; }

    /// <summary>JSON serialized dictionary of original values before modification</summary>
    public string? OldValuesJson { get; set; }

    /// <summary>JSON serialized dictionary of new values after modification</summary>
    public string? NewValuesJson { get; set; }

    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    public string? IpAddress { get; set; }
    public string? Endpoint { get; set; }
}
