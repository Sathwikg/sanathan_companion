namespace Sanathana.Companion.Application.DTOs.AccessRights;

/// <summary>A role that access rights can be configured for (Admin is excluded — it always has full access).</summary>
public class AccessRoleDto
{
    public int RoleId { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public string? Description { get; set; }
}

/// <summary>One form (menu module) and whether the selected role may open it on each platform.</summary>
public class ModuleAccessDto
{
    public Guid ModuleId { get; set; }
    public string ModuleName { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public Guid? ParentId { get; set; }
    public string? ParentName { get; set; }
    /// <summary>True when this module is a container (has children) rather than a navigable form.</summary>
    public bool IsParent { get; set; }
    /// <summary>Whether the form is published to mobile at all (the module's own ShowInMobile flag).</summary>
    public bool ShowInMobile { get; set; }
    public bool WebEnabled { get; set; }
    public bool MobileEnabled { get; set; }
}

/// <summary>The full access matrix for one role.</summary>
public class AccessMatrixDto
{
    public int RoleId { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public List<ModuleAccessDto> Modules { get; set; } = new();
}

/// <summary>One row of the matrix as posted back on save.</summary>
public class ModuleAccessItemDto
{
    public Guid ModuleId { get; set; }
    public bool WebEnabled { get; set; }
    public bool MobileEnabled { get; set; }
}

/// <summary>The desired access state for a role — replaces the stored set.</summary>
public class SaveAccessRightsDto
{
    public List<ModuleAccessItemDto> Items { get; set; } = new();
}
