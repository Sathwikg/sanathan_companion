using Sanathana.Companion.Domain.Common;

namespace Sanathana.Companion.Domain.Entities;

/// <summary>
/// Form-level audit configuration specifying whether navigation/time-spent and data modifications
/// should be logged for a particular module.
/// </summary>
public class AuditModuleConfig : BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid MenuModuleId { get; set; }
    public MenuModule? MenuModule { get; set; }

    /// <summary>The stable module code matching ModuleCodes (e.g. "deities", "chants").</summary>
    public string ModuleCode { get; set; } = string.Empty;

    /// <summary>Whether navigation to this form and time spent on it are recorded.</summary>
    public bool IsActivityAuditEnabled { get; set; } = true;

    /// <summary>Whether data modifications (Insert/Update/Delete) for this entity/module are recorded.</summary>
    public bool IsDataAuditEnabled { get; set; } = true;
}
