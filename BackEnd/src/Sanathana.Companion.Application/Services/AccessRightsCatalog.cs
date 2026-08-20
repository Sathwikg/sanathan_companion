using Microsoft.Extensions.DependencyInjection;
using Sanathana.Companion.Application.Interfaces;
using Sanathana.Companion.Domain.Interfaces;

namespace Sanathana.Companion.Application.Services;

/// <inheritdoc />
/// <remarks>
/// Structurally the same as <see cref="TranslationCatalog"/>: a singleton holding an immutable
/// snapshot behind a <see cref="Lazy{T}"/>, rebuilt only when something invalidates it. That is
/// what makes a per-request authorization check affordable — steady state is one dictionary lookup
/// and one set lookup, with no database round trip.
/// </remarks>
public sealed class AccessRightsCatalog : IAccessRightsCatalog
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly object _gate = new();
    private Lazy<Task<AccessSnapshot>> _snapshot;

    public AccessRightsCatalog(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
        _snapshot = NewLazy();
    }

    public Task<AccessSnapshot> GetAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate) return _snapshot.Value;
    }

    public void Invalidate()
    {
        lock (_gate) _snapshot = NewLazy();
    }

    // ExecutionAndPublication so a burst of concurrent first-requests builds once, not N times.
    private Lazy<Task<AccessSnapshot>> NewLazy()
        => new(() => BuildAsync(CancellationToken.None), LazyThreadSafetyMode.ExecutionAndPublication);

    private async Task<AccessSnapshot> BuildAsync(CancellationToken cancellationToken)
    {
        // The catalog is a singleton but the repositories are scoped, so open our own scope.
        using var scope = _scopeFactory.CreateScope();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var roles = await uow.Roles.ListAllAsync(cancellationToken);
        var mappings = await uow.ModuleRoleMappings.ListAllAsync(cancellationToken);
        var modules = await uow.MenuModules.GetAllOrderedAsync(cancellationToken);

        // Deactivating a form has to close its API too, or "turn this off" would only hide it from
        // the menu. IsVisibleInMenu is deliberately NOT considered: that is presentation, and a
        // form hidden from navigation can still be reached by a direct link.
        var codeByModuleId = modules
            .Where(m => m.IsActive && !string.IsNullOrWhiteSpace(m.Code))
            .ToDictionary(m => m.Id, m => m.Code!);

        var byRole = new Dictionary<string, IReadOnlySet<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var role in roles)
        {
            var granted = new HashSet<string>(StringComparer.Ordinal);

            foreach (var mapping in mappings.Where(m => m.RoleId == role.RoleId))
            {
                // The union of the two platform columns. Platform is client-supplied and
                // unverifiable — the same token signs into either host and the client picks the
                // string — so enforcing the split here would only stop honest callers.
                if (!mapping.WebEnabled && !mapping.MobileEnabled) continue;
                if (codeByModuleId.TryGetValue(mapping.MenuModuleId, out var code)) granted.Add(code);
            }

            byRole[role.RoleName] = granted;
        }

        return new AccessSnapshot(byRole);
    }
}
