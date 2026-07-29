using Sanathana.Companion.Domain.Common;

namespace Sanathana.Companion.Domain.Entities;

/// <summary>
/// A navigation menu entry. Modules and sub-modules share this one table: a row with a
/// null <see cref="ParentId"/> is a main module; a row with a parent is a sub-module.
/// </summary>
public class MenuModule : BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public string? Description { get; set; }
    public string? RoutePath { get; set; }
    public int DisplayOrder { get; set; }

    /// <summary>Whether the item is shown in the navigation menu.</summary>
    public bool IsVisibleInMenu { get; set; } = true;

    /// <summary>Whether the item is exposed in the mobile app.</summary>
    public bool ShowInMobile { get; set; } = true;

    /// <summary>Active / deactivated status.</summary>
    public bool IsActive { get; set; } = true;

    public Guid? ParentId { get; set; }
    public MenuModule? Parent { get; set; }
    public ICollection<MenuModule> Children { get; set; } = new List<MenuModule>();
}
