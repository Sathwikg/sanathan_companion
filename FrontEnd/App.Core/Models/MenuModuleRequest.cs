using System.ComponentModel.DataAnnotations;

namespace App.Core.Models;

/// <summary>Payload for creating/updating a menu item (module or sub-module).</summary>
public class MenuModuleRequest
{
    [Required(ErrorMessage = "Name is required.")]
    [StringLength(150)]
    public string Name { get; set; } = string.Empty;

    [StringLength(100)]
    public string? Icon { get; set; }

    [StringLength(500)]
    public string? Description { get; set; }

    [StringLength(300)]
    public string? RoutePath { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "Display order must be 0 or greater.")]
    public int DisplayOrder { get; set; }

    public bool IsVisibleInMenu { get; set; } = true;
    public bool ShowInMobile { get; set; } = true;
    public bool IsActive { get; set; } = true;

    /// <summary>Select a main module to make this a sub-module; leave empty for a main module.</summary>
    public Guid? ParentId { get; set; }
}
