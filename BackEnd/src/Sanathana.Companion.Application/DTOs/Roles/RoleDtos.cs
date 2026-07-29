namespace Sanathana.Companion.Application.DTOs.Roles;

public class RoleDto
{
    public int RoleId { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>Users currently assigned to this role.</summary>
    public int UserCount { get; set; }

    /// <summary>Forms this role has been granted access to.</summary>
    public int FormCount { get; set; }

    /// <summary>Seeded roles the application depends on — renaming or deleting them is blocked.</summary>
    public bool IsSystemRole { get; set; }

    /// <summary>False when the role is still referenced by users, so the UI can explain why.</summary>
    public bool CanDelete { get; set; }

    public DateTime CreatedDate { get; set; }
    public DateTime? ModifiedDate { get; set; }
}

public class CreateRoleDto
{
    public string RoleName { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class UpdateRoleDto
{
    public string RoleName { get; set; } = string.Empty;
    public string? Description { get; set; }
}
