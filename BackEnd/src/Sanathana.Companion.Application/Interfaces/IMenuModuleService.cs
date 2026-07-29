using Sanathana.Companion.Application.DTOs.Menu;

namespace Sanathana.Companion.Application.Interfaces;

public interface IMenuModuleService
{
    Task<IReadOnlyList<MenuModuleDto>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Full tree of all items (management tree view).</summary>
    Task<List<MenuTreeNodeDto>> GetTreeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Tree of active + visible items for the navigation sidebar, filtered to what the given role
    /// may access on the given platform. Admin always sees every form. Non-Admin roles see only the
    /// forms enabled for them (web vs mobile) on the Access Rights screen.
    /// </summary>
    /// <param name="platform">"Web" or "Mobile" (defaults to Web when null/unknown).</param>
    /// <param name="roleName">The caller's role name (from the JWT).</param>
    Task<List<MenuTreeNodeDto>> GetMenuAsync(string? platform, string? roleName, CancellationToken cancellationToken = default);

    Task<MenuModuleDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Guid> CreateAsync(CreateMenuModuleDto dto, CancellationToken cancellationToken = default);
    Task UpdateAsync(Guid id, UpdateMenuModuleDto dto, CancellationToken cancellationToken = default);
    Task SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken = default);
}
