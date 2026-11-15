using App.Core.Models;
using App.Core.Services;

namespace App.Tests;

/// <summary>Panchangam in the bottom bar, and the phone's text size kept within sane limits.</summary>
public class MobileShellTests
{
    private static MenuTreeNode Node(string name, string? route, params MenuTreeNode[] children)
        => new() { Id = Guid.NewGuid(), Name = name, RoutePath = route, Children = children.ToList() };

    /// <summary>The seeker's phone menu as the live database ordered it on 2026-11-15.</summary>
    private static List<MenuTreeNode> SeekerMenu() =>
    [
        Node("Mobile Home", "/mobile-dashboard"),
        Node("Sadhana", null,
            Node("Today's Sadhana", "/sadhana"),
            Node("Calendar", "/calendar"),
            Node("Download Wallpapers", "/wallpapers-download"),
            Node("Panchangam", "/panchangam"),
            Node("Puja Process", "/puja-process")),
        Node("Feedback", null, Node("Feedback Form", "/feedback")),
        Node("Favorites", "/favorites"),
    ];

    private static List<string> Tabs(IReadOnlyList<MenuTreeNode> menu)
        => MobileMenu.Build(menu).Primary.Select(n => n.Name).ToList();

    [Fact]
    public void Panchangam_takes_a_tab_even_when_the_menu_orders_it_fifth()
    {
        var plan = MobileMenu.Build(SeekerMenu());

        Assert.Equal(["Mobile Home", "Today's Sadhana", "Calendar", "Panchangam"], plan.Primary.Select(n => n.Name).ToList());
        // What it displaced is one tap away, not gone.
        Assert.Contains(plan.Secondary, n => n.Name == "Download Wallpapers");
    }

    [Fact]
    public void Panchangam_already_among_the_tabs_keeps_its_place()
    {
        List<MenuTreeNode> menu =
        [
            Node("Mobile Home", "/mobile-dashboard"),
            Node("Panchangam", "/panchangam"),
            Node("Today's Sadhana", "/sadhana"),
            Node("Calendar", "/calendar"),
            Node("Favorites", "/favorites"),
        ];

        Assert.Equal(["Mobile Home", "Panchangam", "Today's Sadhana", "Calendar"], Tabs(menu));
    }

    [Fact]
    public void A_role_without_panchangam_gets_its_menu_as_ordered()
    {
        List<MenuTreeNode> menu =
        [
            Node("Mobile Home", "/mobile-dashboard"),
            Node("Today's Sadhana", "/sadhana"),
            Node("Calendar", "/calendar"),
            Node("Favorites", "/favorites"),
            Node("Feedback Form", "/feedback"),
        ];

        Assert.Equal(["Mobile Home", "Today's Sadhana", "Calendar", "Favorites"], Tabs(menu));
    }

    [Fact]
    public void A_look_alike_route_is_not_mistaken_for_panchangam()
    {
        List<MenuTreeNode> menu =
        [
            Node("Mobile Home", "/mobile-dashboard"),
            Node("A", "/a"), Node("B", "/b"), Node("C", "/c"),
            Node("Panchangam Admin", "/panchangam-admin"),
        ];

        Assert.DoesNotContain("Panchangam Admin", Tabs(menu));
    }

    [Fact]
    public void The_first_tab_is_never_given_up()
    {
        List<MenuTreeNode> menu = [Node("Mobile Home", "/mobile-dashboard"), Node("A", "/a"), Node("Panchangam", "/panchangam")];

        Assert.Equal(["Mobile Home"], MobileMenu.Build(menu, slots: 1).Primary.Select(n => n.Name).ToList());
    }

    [Theory]
    [InlineData(1.0, 1.0)]
    [InlineData(1.3, 1.3)]
    [InlineData(1.85, 1.85)]
    [InlineData(0.5, FontScaleLimits.Min)]       // smaller than legible
    [InlineData(3.1, FontScaleLimits.Max)]       // iOS's largest Accessibility size
    [InlineData(0.0, 1.0)]                       // the platform could not say
    [InlineData(double.NaN, 1.0)]
    public void The_phones_text_size_is_followed_within_limits(double setting, double applied)
        => Assert.Equal(applied, FontScaleLimits.Clamp(setting));
}
