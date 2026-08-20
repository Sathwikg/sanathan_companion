namespace Sanathana.Companion.Application.Interfaces;

/// <summary>Which module codes each role may reach, cached until something changes them.</summary>
public interface IAccessRightsCatalog
{
    Task<AccessSnapshot> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>Drops the snapshot. Called whenever a grant, a role or a module is written.</summary>
    void Invalidate();
}

/// <summary>
/// An immutable view of the access matrix: role name to the set of module codes it may reach.
/// </summary>
/// <remarks>
/// Keyed by role NAME rather than id because that is what a JWT carries, and compared
/// case-insensitively for the same reason.
/// </remarks>
public sealed class AccessSnapshot
{
    private readonly IReadOnlyDictionary<string, IReadOnlySet<string>> _byRole;

    public AccessSnapshot(IReadOnlyDictionary<string, IReadOnlySet<string>> byRole) => _byRole = byRole;

    public bool Allows(string? roleName, IReadOnlyList<string> anyOfCodes)
    {
        if (string.IsNullOrWhiteSpace(roleName)) return false;
        if (!_byRole.TryGetValue(roleName, out var granted)) return false;

        for (var i = 0; i < anyOfCodes.Count; i++)
            if (granted.Contains(anyOfCodes[i]))
                return true;

        return false;
    }
}
