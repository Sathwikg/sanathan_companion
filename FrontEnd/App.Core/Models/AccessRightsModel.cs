namespace App.Core.Models;

/// <summary>A role that access rights can be configured for (Admin is excluded — it always has full access).</summary>
public class AccessRoleModel
{
    public int RoleId { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public string? Description { get; set; }
}

/// <summary>One form (menu module) plus whether the selected role may open it on each platform.</summary>
public class ModuleAccessModel
{
    public Guid ModuleId { get; set; }
    public string ModuleName { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public Guid? ParentId { get; set; }
    public string? ParentName { get; set; }
    public bool IsParent { get; set; }
    public bool ShowInMobile { get; set; }
    public bool WebEnabled { get; set; }
    public bool MobileEnabled { get; set; }
}

/// <summary>The full access matrix for one role.</summary>
public class AccessMatrixModel
{
    public int RoleId { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public List<ModuleAccessModel> Modules { get; set; } = new();
}

/// <summary>One matrix row posted back on save.</summary>
public class ModuleAccessItem
{
    public Guid ModuleId { get; set; }
    public bool WebEnabled { get; set; }
    public bool MobileEnabled { get; set; }
}

/// <summary>Save payload — the desired access state for a role.</summary>
public class SaveAccessRightsRequest
{
    public List<ModuleAccessItem> Items { get; set; } = new();
}
