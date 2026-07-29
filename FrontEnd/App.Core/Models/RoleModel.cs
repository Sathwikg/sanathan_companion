using System.ComponentModel.DataAnnotations;

namespace App.Core.Models;

public class RoleModel
{
    public int RoleId { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int UserCount { get; set; }
    public int FormCount { get; set; }

    /// <summary>Built-in roles the application depends on — they cannot be renamed or deleted.</summary>
    public bool IsSystemRole { get; set; }

    public bool CanDelete { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime? ModifiedDate { get; set; }
}

public class RoleRequest
{
    [Required(ErrorMessage = "Role name is required.")]
    [StringLength(50)]
    public string RoleName { get; set; } = string.Empty;

    [StringLength(250)]
    public string? Description { get; set; }
}
