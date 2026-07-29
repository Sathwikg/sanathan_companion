using Sanathana.Companion.Domain.Common;

namespace Sanathana.Companion.Domain.Entities;

/// <summary>
/// Grants a role access to a menu module (form), per platform.
/// One row per (Role, MenuModule). Absence of a row means "no access" for that role.
/// The Admin role is never stored here — it always has access to every form.
/// </summary>
public class ModuleRoleMapping : BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>FK to <see cref="Role.RoleId"/> (int).</summary>
    public int RoleId { get; set; }
    public Role? Role { get; set; }

    /// <summary>FK to <see cref="MenuModule.Id"/> (Guid).</summary>
    public Guid MenuModuleId { get; set; }
    public MenuModule? MenuModule { get; set; }

    /// <summary>The role can open this form in the web app.</summary>
    public bool WebEnabled { get; set; }

    /// <summary>The role can open this form in the mobile app.</summary>
    public bool MobileEnabled { get; set; }
}
