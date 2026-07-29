namespace Sanathana.Companion.Application.DTOs.Menu;

/// <summary>Flat representation used by the management list view.</summary>
public class MenuModuleDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public string? Description { get; set; }
    public string? RoutePath { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsVisibleInMenu { get; set; }
    public bool ShowInMobile { get; set; }
    public bool IsActive { get; set; }
    public Guid? ParentId { get; set; }
    public string? ParentName { get; set; }
}

/// <summary>Hierarchical node used by the sidebar and the management tree view.</summary>
public class MenuTreeNodeDto
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
    public List<MenuTreeNodeDto> Children { get; set; } = new();
}

public class CreateMenuModuleDto
{
    public string Name { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public string? Description { get; set; }
    public string? RoutePath { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsVisibleInMenu { get; set; } = true;
    public bool ShowInMobile { get; set; } = true;
    public bool IsActive { get; set; } = true;

    /// <summary>Select a main module to make this record its sub-module; null = main module.</summary>
    public Guid? ParentId { get; set; }
}

public class UpdateMenuModuleDto
{
    public string Name { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public string? Description { get; set; }
    public string? RoutePath { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsVisibleInMenu { get; set; } = true;
    public bool ShowInMobile { get; set; } = true;
    public bool IsActive { get; set; } = true;
    public Guid? ParentId { get; set; }
}

public class UpdateMenuStatusDto
{
    public bool IsActive { get; set; }
}
