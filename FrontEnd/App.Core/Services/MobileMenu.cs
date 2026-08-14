using App.Core.Models;

namespace App.Core.Services;

/// <summary>
/// Turns the menu the server returned into the two surfaces a phone has room for: a handful of
/// tabs along the bottom, and everything else behind "More".
/// </summary>
/// <remarks>
/// Deliberately a pure function over the API's tree rather than a hardcoded tab list. The menu is
/// already ordered, role-filtered and platform-filtered server-side, so an administrator who
/// reorders modules or publishes a new one to mobile changes the bottom bar without an app update.
/// </remarks>
public static class MobileMenu
{
    /// <summary>
    /// Tabs before "More". Five targets is the practical ceiling on a phone — past that each one is
    /// narrower than a fingertip — and the fifth is spent on "More" itself.
    /// </summary>
    public const int PrimarySlots = 4;

    /// <summary>The home route, pinned to the first tab wherever it appears in the tree.</summary>
    private const string HomeRoute = "/";

    /// <param name="Primary">Leaf items to render as bottom tabs, in order.</param>
    /// <param name="Secondary">Every other navigable leaf, for the "More" sheet's quick grid.</param>
    /// <param name="Tree">The untouched tree, for the "More" sheet's grouped list.</param>
    public sealed record Plan(
        IReadOnlyList<MenuTreeNode> Primary,
        IReadOnlyList<MenuTreeNode> Secondary,
        IReadOnlyList<MenuTreeNode> Tree);

    public static Plan Build(IReadOnlyList<MenuTreeNode>? tree, int slots = PrimarySlots)
    {
        var nodes = tree ?? Array.Empty<MenuTreeNode>();
        var leaves = Leaves(nodes).ToList();

        // Home first if it is in the menu at all: it is the one destination a seeker reaches for
        // without reading, so it should not drift with DisplayOrder.
        var home = leaves.FirstOrDefault(IsHome);
        if (home is not null)
        {
            leaves.Remove(home);
            leaves.Insert(0, home);
        }

        var take = Math.Clamp(slots, 0, leaves.Count);
        return new Plan(
            leaves.Take(take).ToList(),
            leaves.Skip(take).ToList(),
            nodes);
    }

    /// <summary>
    /// Depth-first so a group's children follow it, which is the order the sidebar shows and the
    /// order an administrator sees when setting DisplayOrder.
    /// </summary>
    public static IEnumerable<MenuTreeNode> Leaves(IEnumerable<MenuTreeNode> nodes)
    {
        foreach (var node in nodes)
        {
            if (node.Children.Count > 0)
            {
                foreach (var child in Leaves(node.Children)) yield return child;
            }
            // A group with no route is pure navigation and cannot be a tab; a childless node
            // without a route is a misconfiguration and would render a tab that goes nowhere.
            else if (!string.IsNullOrWhiteSpace(node.RoutePath))
            {
                yield return node;
            }
        }
    }

    /// <summary>Route stripped of its leading slash, the form Blazor's <c>href</c> wants.</summary>
    public static string Href(MenuTreeNode node) => (node.RoutePath ?? string.Empty).TrimStart('/');

    public static bool IsHome(MenuTreeNode node) => Normalise(node.RoutePath).Length == 0
        && !string.IsNullOrWhiteSpace(node.RoutePath);

    /// <summary>
    /// Segment-aware so "/chants" does not light up while the user is on "/chants-config".
    /// Mirrors <c>NavTreeItem.RouteMatches</c> — the two must agree or the sidebar and the bottom
    /// bar would disagree about which page is current.
    /// </summary>
    public static bool IsActive(MenuTreeNode node, string currentPath)
    {
        var route = Normalise(node.RoutePath);
        var current = Normalise(currentPath);

        if (route.Length == 0)
            return current.Length == 0 && !string.IsNullOrWhiteSpace(node.RoutePath);

        return current.Equals(route, StringComparison.OrdinalIgnoreCase)
            || current.StartsWith(route + "/", StringComparison.OrdinalIgnoreCase);
    }

    private static string Normalise(string? path)
    {
        var value = path ?? string.Empty;
        var cut = value.IndexOfAny(new[] { '?', '#' });
        if (cut >= 0) value = value[..cut];
        return value.Trim().Trim('/');
    }
}
