using FluentValidation;
using Sanathana.Companion.Application.Common;
using Sanathana.Companion.Application.DTOs.Menu;
using Sanathana.Companion.Application.Interfaces;
using Sanathana.Companion.Domain.Entities;
using Sanathana.Companion.Domain.Exceptions;
using Sanathana.Companion.Domain.Interfaces;

namespace Sanathana.Companion.Application.Services;

public class MenuModuleService : IMenuModuleService
{
    private readonly IUnitOfWork _uow;
    private readonly IValidator<CreateMenuModuleDto> _createValidator;
    private readonly IValidator<UpdateMenuModuleDto> _updateValidator;

    public MenuModuleService(
        IUnitOfWork uow,
        IValidator<CreateMenuModuleDto> createValidator,
        IValidator<UpdateMenuModuleDto> updateValidator)
    {
        _uow = uow;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    public async Task<IReadOnlyList<MenuModuleDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var items = await _uow.MenuModules.GetAllOrderedAsync(cancellationToken);
        var byId = items.ToDictionary(m => m.Id);
        return items.Select(m => ToDto(m, byId)).ToList();
    }

    public async Task<List<MenuTreeNodeDto>> GetTreeAsync(CancellationToken cancellationToken = default)
    {
        var items = await _uow.MenuModules.GetAllOrderedAsync(cancellationToken);
        return BuildTree(items, requireParentPresent: false);
    }

    public async Task<List<MenuTreeNodeDto>> GetMenuAsync(string? platform, string? roleName, CancellationToken cancellationToken = default)
    {
        var all = await _uow.MenuModules.GetAllOrderedAsync(cancellationToken);
        var candidates = all.Where(m => m.IsActive && m.IsVisibleInMenu).ToList();

        // Admin always sees every form — no per-role filtering.
        if (string.Equals(roleName, RoleNames.Admin, StringComparison.OrdinalIgnoreCase))
            return BuildTree(candidates, requireParentPresent: true);

        // Non-Admin: keep only forms this role may access on this platform (default-deny).
        var mappings = new Dictionary<Guid, ModuleRoleMapping>();
        if (!string.IsNullOrWhiteSpace(roleName))
        {
            var role = await _uow.Roles.GetByNameAsync(roleName!, cancellationToken);
            if (role is not null)
                foreach (var mr in await _uow.ModuleRoleMappings.GetByRoleAsync(role.RoleId, cancellationToken))
                    mappings[mr.MenuModuleId] = mr;
        }

        var isMobile = string.Equals(platform, "Mobile", StringComparison.OrdinalIgnoreCase);
        var parentIds = all.Where(m => m.ParentId.HasValue).Select(m => m.ParentId!.Value).ToHashSet();

        bool LeafAllowed(MenuModule m)
            => mappings.TryGetValue(m.Id, out var f) && (isMobile ? f.MobileEnabled : f.WebEnabled);

        // Leaf forms are visible when the role is granted them; a container is visible when it has a visible child.
        var visible = new HashSet<Guid>();
        foreach (var m in candidates)
            if (!parentIds.Contains(m.Id) && LeafAllowed(m))
                visible.Add(m.Id);
        foreach (var m in candidates)
            if (parentIds.Contains(m.Id) && candidates.Any(c => c.ParentId == m.Id && visible.Contains(c.Id)))
                visible.Add(m.Id);

        var filtered = candidates.Where(m => visible.Contains(m.Id)).ToList();
        return BuildTree(filtered, requireParentPresent: true);
    }

    public async Task<MenuModuleDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _uow.MenuModules.GetByIdAsync(id, cancellationToken);
        if (entity is null) return null;

        string? parentName = null;
        if (entity.ParentId.HasValue)
            parentName = (await _uow.MenuModules.GetByIdAsync(entity.ParentId.Value, cancellationToken))?.Name;

        return ToDto(entity, parentName);
    }

    public async Task<Guid> CreateAsync(CreateMenuModuleDto dto, CancellationToken cancellationToken = default)
    {
        await _createValidator.ValidateAndThrowAsync(dto, cancellationToken);
        await ValidateParentAsync(dto.ParentId, currentId: null, cancellationToken);

        var entity = new MenuModule
        {
            Id = Guid.NewGuid(),
            Name = dto.Name.Trim(),
            Icon = Clean(dto.Icon),
            Description = Clean(dto.Description),
            RoutePath = Clean(dto.RoutePath),
            DisplayOrder = dto.DisplayOrder,
            IsVisibleInMenu = dto.IsVisibleInMenu,
            ShowInMobile = dto.ShowInMobile,
            IsActive = dto.IsActive,
            ParentId = dto.ParentId
        };

        await _uow.MenuModules.AddAsync(entity, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }

    public async Task UpdateAsync(Guid id, UpdateMenuModuleDto dto, CancellationToken cancellationToken = default)
    {
        await _updateValidator.ValidateAndThrowAsync(dto, cancellationToken);

        var entity = await _uow.MenuModules.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Menu item '{id}' was not found.");

        await ValidateParentAsync(dto.ParentId, currentId: id, cancellationToken);

        entity.Name = dto.Name.Trim();
        entity.Icon = Clean(dto.Icon);
        entity.Description = Clean(dto.Description);
        entity.RoutePath = Clean(dto.RoutePath);
        entity.DisplayOrder = dto.DisplayOrder;
        entity.IsVisibleInMenu = dto.IsVisibleInMenu;
        entity.ShowInMobile = dto.ShowInMobile;
        entity.IsActive = dto.IsActive;
        entity.ParentId = dto.ParentId;

        _uow.MenuModules.Update(entity);
        await _uow.SaveChangesAsync(cancellationToken);
    }

    public async Task SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken = default)
    {
        var entity = await _uow.MenuModules.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Menu item '{id}' was not found.");

        entity.IsActive = isActive;
        _uow.MenuModules.Update(entity);
        await _uow.SaveChangesAsync(cancellationToken);
    }

    private async Task ValidateParentAsync(Guid? parentId, Guid? currentId, CancellationToken cancellationToken)
    {
        if (parentId is null) return;

        if (currentId.HasValue && parentId.Value == currentId.Value)
            throw new BadRequestException("A menu item cannot be its own parent.");

        var parent = await _uow.MenuModules.GetByIdAsync(parentId.Value, cancellationToken)
            ?? throw new BadRequestException("The selected parent module does not exist.");

        if (parent.ParentId is not null)
            throw new BadRequestException("Sub-modules can only be nested under a main module.");
    }

    private static List<MenuTreeNodeDto> BuildTree(IReadOnlyList<MenuModule> items, bool requireParentPresent)
    {
        var nodes = items.ToDictionary(m => m.Id, ToNode);
        var roots = new List<MenuTreeNodeDto>();

        foreach (var m in items)
        {
            var node = nodes[m.Id];
            if (m.ParentId is null)
                roots.Add(node);
            else if (nodes.TryGetValue(m.ParentId.Value, out var parent))
                parent.Children.Add(node);
            else if (!requireParentPresent)
                roots.Add(node); // orphan → show as root when listing everything
            // else: parent filtered out of the menu → drop this branch
        }

        return roots;
    }

    private static MenuTreeNodeDto ToNode(MenuModule m) => new()
    {
        Id = m.Id,
        Name = m.Name,
        Icon = m.Icon,
        RoutePath = m.RoutePath,
        DisplayOrder = m.DisplayOrder,
        IsVisibleInMenu = m.IsVisibleInMenu,
        ShowInMobile = m.ShowInMobile,
        IsActive = m.IsActive,
        ParentId = m.ParentId
    };

    private static MenuModuleDto ToDto(MenuModule m, IReadOnlyDictionary<Guid, MenuModule> byId)
        => ToDto(m, m.ParentId.HasValue && byId.TryGetValue(m.ParentId.Value, out var p) ? p.Name : null);

    private static MenuModuleDto ToDto(MenuModule m, string? parentName) => new()
    {
        Id = m.Id,
        Name = m.Name,
        Icon = m.Icon,
        Description = m.Description,
        RoutePath = m.RoutePath,
        DisplayOrder = m.DisplayOrder,
        IsVisibleInMenu = m.IsVisibleInMenu,
        ShowInMobile = m.ShowInMobile,
        IsActive = m.IsActive,
        ParentId = m.ParentId,
        ParentName = parentName
    };

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
