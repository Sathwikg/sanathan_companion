using Sanathana.Companion.Application.Common;
using Sanathana.Companion.Application.DTOs.AccessRights;
using Sanathana.Companion.Application.Interfaces;
using Sanathana.Companion.Domain.Entities;
using Sanathana.Companion.Domain.Exceptions;
using Sanathana.Companion.Domain.Interfaces;

namespace Sanathana.Companion.Application.Services;

public class AccessRightsService : IAccessRightsService
{
    private readonly IUnitOfWork _uow;

    public AccessRightsService(IUnitOfWork uow) => _uow = uow;

    public async Task<IReadOnlyList<AccessRoleDto>> GetAssignableRolesAsync(CancellationToken cancellationToken = default)
    {
        var roles = await _uow.Roles.ListAllAsync(cancellationToken);
        return roles
            .Where(r => !string.Equals(r.RoleName, RoleNames.Admin, StringComparison.OrdinalIgnoreCase))
            .OrderBy(r => r.RoleName)
            .Select(r => new AccessRoleDto { RoleId = r.RoleId, RoleName = r.RoleName, Description = r.Description })
            .ToList();
    }

    public async Task<AccessMatrixDto> GetMatrixAsync(int roleId, CancellationToken cancellationToken = default)
    {
        var role = await RequireAssignableRoleAsync(roleId, cancellationToken);

        var modules = await _uow.MenuModules.GetAllOrderedAsync(cancellationToken);
        var byId = modules.ToDictionary(m => m.Id);
        var parentIds = modules.Where(m => m.ParentId.HasValue).Select(m => m.ParentId!.Value).ToHashSet();

        var mappings = (await _uow.ModuleRoleMappings.GetByRoleAsync(role.RoleId, cancellationToken))
            .ToDictionary(x => x.MenuModuleId);

        var dto = new AccessMatrixDto { RoleId = role.RoleId, RoleName = role.RoleName };
        foreach (var m in OrderForDisplay(modules))
        {
            mappings.TryGetValue(m.Id, out var access);
            dto.Modules.Add(new ModuleAccessDto
            {
                ModuleId = m.Id,
                ModuleName = m.Name,
                Icon = m.Icon,
                ParentId = m.ParentId,
                ParentName = m.ParentId.HasValue && byId.TryGetValue(m.ParentId.Value, out var p) ? p.Name : null,
                IsParent = parentIds.Contains(m.Id),
                ShowInMobile = m.ShowInMobile,
                WebEnabled = access?.WebEnabled ?? false,
                MobileEnabled = access?.MobileEnabled ?? false
            });
        }
        return dto;
    }

    public async Task SaveMatrixAsync(int roleId, SaveAccessRightsDto dto, CancellationToken cancellationToken = default)
    {
        var role = await RequireAssignableRoleAsync(roleId, cancellationToken);

        var validModuleIds = (await _uow.MenuModules.GetAllOrderedAsync(cancellationToken)).Select(m => m.Id).ToHashSet();
        var existing = (await _uow.ModuleRoleMappings.GetByRoleTrackedAsync(role.RoleId, cancellationToken))
            .ToDictionary(x => x.MenuModuleId);

        foreach (var item in dto.Items)
        {
            if (!validModuleIds.Contains(item.ModuleId))
                throw new BadRequestException($"Form '{item.ModuleId}' does not exist.");

            var enabled = item.WebEnabled || item.MobileEnabled;
            if (existing.TryGetValue(item.ModuleId, out var row))
            {
                if (!enabled)
                {
                    _uow.ModuleRoleMappings.Remove(row);
                }
                else
                {
                    row.WebEnabled = item.WebEnabled;
                    row.MobileEnabled = item.MobileEnabled;
                    _uow.ModuleRoleMappings.Update(row);
                }
            }
            else if (enabled)
            {
                await _uow.ModuleRoleMappings.AddAsync(new ModuleRoleMapping
                {
                    Id = Guid.NewGuid(),
                    RoleId = role.RoleId,
                    MenuModuleId = item.ModuleId,
                    WebEnabled = item.WebEnabled,
                    MobileEnabled = item.MobileEnabled
                }, cancellationToken);
            }
        }

        await _uow.SaveChangesAsync(cancellationToken);
    }

    private async Task<Role> RequireAssignableRoleAsync(int roleId, CancellationToken cancellationToken)
    {
        var role = await _uow.Roles.GetByIdAsync(roleId, cancellationToken)
            ?? throw new NotFoundException($"Role '{roleId}' was not found.");

        if (string.Equals(role.RoleName, RoleNames.Admin, StringComparison.OrdinalIgnoreCase))
            throw new BadRequestException("The Admin role always has full access and cannot be restricted.");

        return role;
    }

    /// <summary>
    /// Flattens modules so each top-level module is immediately followed by its children.
    /// Guarantees EVERY module is emitted exactly once — deeper-than-2-level nesting or
    /// orphaned parents are appended at the end rather than silently dropped.
    /// </summary>
    private static IEnumerable<MenuModule> OrderForDisplay(IReadOnlyList<MenuModule> modules)
    {
        var emitted = new HashSet<Guid>();
        var roots = modules.Where(m => m.ParentId is null).OrderBy(m => m.DisplayOrder).ThenBy(m => m.Name);
        foreach (var root in roots)
        {
            if (emitted.Add(root.Id)) yield return root;
            foreach (var child in modules.Where(m => m.ParentId == root.Id).OrderBy(m => m.DisplayOrder).ThenBy(m => m.Name))
                if (emitted.Add(child.Id)) yield return child;
        }
        // Anything not reached above (deeper nesting / missing parent) — never hide a form.
        foreach (var rest in modules.Where(m => !emitted.Contains(m.Id)).OrderBy(m => m.DisplayOrder).ThenBy(m => m.Name))
            yield return rest;
    }
}
