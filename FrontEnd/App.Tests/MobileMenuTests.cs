using App.Core.Models;
using App.Core.Services;

namespace App.Tests;

/// <summary>
/// The bottom bar is generated from the server's menu, so the rule that picks its tabs is the
/// only thing standing between an administrator's DisplayOrder and what a seeker's thumb finds.
/// </summary>
public class MobileMenuTests
{
    private static MenuTreeNode Node(string name, string? route, params MenuTreeNode[] children)
        => new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            RoutePath = route,
            Children = children.ToList()
        };

    private static List<MenuTreeNode> SampleMenu() =>
    [
        Node("User Dashboard", "/"),
        Node("Masters", null,
            Node("Festivals", "/festivals"),
            Node("Deities", "/deities"),
            Node("Chants", "/chants")),
        Node("Sadhana", null,
            Node("Today's Sadhana", "/sadhana"),
            Node("Download Wallpapers", "/wallpapers-download")),
    ];

    [Fact]
    public void Leaves_are_flattened_depth_first_in_tree_order()
    {
        var names = MobileMenu.Leaves(SampleMenu()).Select(n => n.Name).ToList();

        Assert.Equal(
            ["User Dashboard", "Festivals", "Deities", "Chants", "Today's Sadhana", "Download Wallpapers"],
            names);
    }

    [Fact]
    public void Groups_and_routeless_nodes_are_never_tabs()
    {
        var leaves = MobileMenu.Leaves(SampleMenu()).ToList();

        Assert.DoesNotContain(leaves, n => n.Name is "Masters" or "Sadhana");
        Assert.All(leaves, n => Assert.False(string.IsNullOrWhiteSpace(n.RoutePath)));
    }

    [Fact]
    public void A_childless_node_with_no_route_is_dropped_rather_than_rendered_as_a_dead_tab()
    {
        var menu = new List<MenuTreeNode> { Node("Orphan", null), Node("Real", "/real") };

        var leaves = MobileMenu.Leaves(menu).ToList();

        Assert.Equal("Real", Assert.Single(leaves).Name);
    }

    [Fact]
    public void Primary_takes_the_first_slots_and_secondary_takes_the_rest()
    {
        var plan = MobileMenu.Build(SampleMenu());

        Assert.Equal(MobileMenu.PrimarySlots, plan.Primary.Count);
        Assert.Equal(["User Dashboard", "Festivals", "Deities", "Chants"], plan.Primary.Select(n => n.Name));
        Assert.Equal(["Today's Sadhana", "Download Wallpapers"], plan.Secondary.Select(n => n.Name));
    }

    [Fact]
    public void Home_is_pinned_to_the_first_tab_wherever_it_sits_in_the_tree()
    {
        // DisplayOrder put the dashboard last; it still has to be the first thing a thumb finds.
        var menu = new List<MenuTreeNode>
        {
            Node("Festivals", "/festivals"),
            Node("Deities", "/deities"),
            Node("User Dashboard", "/"),
        };

        var plan = MobileMenu.Build(menu);

        Assert.Equal("User Dashboard", plan.Primary[0].Name);
        Assert.Equal(["User Dashboard", "Festivals", "Deities"], plan.Primary.Select(n => n.Name));
    }

    [Fact]
    public void A_short_menu_produces_no_empty_tabs()
    {
        var plan = MobileMenu.Build([Node("Only", "/only")]);

        Assert.Single(plan.Primary);
        Assert.Empty(plan.Secondary);
    }

    [Fact]
    public void An_empty_or_missing_menu_is_not_an_error()
    {
        foreach (var plan in new[] { MobileMenu.Build(null), MobileMenu.Build([]) })
        {
            Assert.Empty(plan.Primary);
            Assert.Empty(plan.Secondary);
            Assert.Empty(plan.Tree);
        }
    }

    [Fact]
    public void Href_drops_the_leading_slash_so_blazor_resolves_it_against_the_base()
    {
        Assert.Equal("festivals", MobileMenu.Href(Node("F", "/festivals")));
        Assert.Equal("", MobileMenu.Href(Node("Home", "/")));
        Assert.Equal("", MobileMenu.Href(Node("Group", null)));
    }

    [Theory]
    // A prefix match must respect segment boundaries, or "/chants" claims the config page too.
    [InlineData("/chants", "chants", true)]
    [InlineData("/chants", "chants/abc", true)]
    [InlineData("/chants", "chants-config", false)]
    [InlineData("/chants", "", false)]
    [InlineData("/sadhana", "sadhana/chant/8f0d", true)]
    public void IsActive_matches_whole_segments_only(string route, string current, bool expected)
        => Assert.Equal(expected, MobileMenu.IsActive(Node("n", route), current));

    [Fact]
    public void Home_is_active_only_on_the_root_path()
    {
        var home = Node("Home", "/");

        Assert.True(MobileMenu.IsActive(home, ""));
        Assert.True(MobileMenu.IsActive(home, "/"));
        Assert.False(MobileMenu.IsActive(home, "festivals"));
    }

    [Fact]
    public void A_routeless_group_is_never_the_active_tab()
    {
        // Guards the empty-string trap: a null route normalises to "" and would otherwise match
        // the home path and light up every group at once.
        var group = Node("Masters", null);

        Assert.False(MobileMenu.IsActive(group, ""));
        Assert.False(MobileMenu.IsActive(group, "festivals"));
        Assert.False(MobileMenu.IsHome(group));
        Assert.True(MobileMenu.IsHome(Node("Home", "/")));
    }

    [Fact]
    public void Query_strings_and_fragments_do_not_break_the_match()
    {
        Assert.True(MobileMenu.IsActive(Node("P", "/panchangam"), "panchangam?date=2026-03-09"));
        Assert.True(MobileMenu.IsActive(Node("P", "/panchangam"), "panchangam#today"));
    }
}
