namespace App.Core.Models;

public class MenuTreeNode
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public string? RoutePath { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsVisibleInMenu { get; set; }
    public bool ShowInMobile { get; set; }
    public bool IsActive { get; set; }
    public Guid? ParentId { get; set; }
    public List<MenuTreeNode> Children { get; set; } = new();
}
