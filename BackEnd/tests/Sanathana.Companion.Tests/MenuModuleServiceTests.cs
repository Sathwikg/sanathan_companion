using Sanathana.Companion.Application.DTOs.Menu;
using Sanathana.Companion.Application.Services;
using Sanathana.Companion.Application.Validators;
using Sanathana.Companion.Domain.Exceptions;

namespace Sanathana.Companion.Tests;

public class MenuModuleServiceTests
{
    private static MenuModuleService NewService(TestHarness harness)
        => new(harness.UnitOfWork, new CreateMenuModuleValidator(), new UpdateMenuModuleValidator());

    [Fact]
    public async Task Dashboards_are_seeded_as_main_menus()
    {
        using var harness = new TestHarness();
        var service = NewService(harness);

        var all = await service.GetAllAsync();
        Assert.Contains(all, m => m.Name == "User Dashboard" && m.ParentId == null && m.IsActive);
        Assert.Contains(all, m => m.Name == "Admin Dashboard" && m.ParentId == null && m.IsActive);

        // Admin sees both dashboards in the navigation menu.
        var menu = await service.GetMenuAsync("Web", "Admin");
        Assert.Contains(menu, n => n.Name == "User Dashboard");
        Assert.Contains(menu, n => n.Name == "Admin Dashboard");
    }

    [Fact]
    public async Task Create_main_then_submodule_builds_tree()
    {
        using var harness = new TestHarness();
        var service = NewService(harness);

        var mainId = await service.CreateAsync(new CreateMenuModuleDto { Name = "Sadhana", DisplayOrder = 2 });
        await service.CreateAsync(new CreateMenuModuleDto { Name = "Mantras", DisplayOrder = 1, ParentId = mainId });

        var tree = await service.GetTreeAsync();
        // target the created module by id — the seed also contains a "Sadhana" module.
        var main = Assert.Single(tree, n => n.Id == mainId);
        var child = Assert.Single(main.Children);
        Assert.Equal("Mantras", child.Name);
        Assert.Equal(mainId, child.ParentId);
    }

    [Fact]
    public async Task Submodule_under_submodule_is_rejected()
    {
        using var harness = new TestHarness();
        var service = NewService(harness);

        var mainId = await service.CreateAsync(new CreateMenuModuleDto { Name = "Sadhana" });
        var subId = await service.CreateAsync(new CreateMenuModuleDto { Name = "Mantras", ParentId = mainId });

        await Assert.ThrowsAsync<BadRequestException>(
            () => service.CreateAsync(new CreateMenuModuleDto { Name = "Deep", ParentId = subId }));
    }

    [Fact]
    public async Task Deactivated_item_is_hidden_from_menu_but_present_in_list()
    {
        using var harness = new TestHarness();
        var service = NewService(harness);

        var id = await service.CreateAsync(new CreateMenuModuleDto { Name = "Festivals", IsActive = true, IsVisibleInMenu = true });
        await service.SetActiveAsync(id, false);

        var menu = await service.GetMenuAsync("Web", "Admin");
        Assert.DoesNotContain(menu, n => n.Name == "Festivals");

        var all = await service.GetAllAsync();
        Assert.Contains(all, m => m.Name == "Festivals" && !m.IsActive);
    }

    [Fact]
    public async Task Mobile_menu_hides_forms_not_published_to_mobile_even_from_admin()
    {
        using var harness = new TestHarness();
        var service = NewService(harness);

        var webOnly = await service.CreateAsync(new CreateMenuModuleDto { Name = "Desk Only", RoutePath = "/desk-only", ShowInMobile = false });
        var both = await service.CreateAsync(new CreateMenuModuleDto { Name = "Everywhere", RoutePath = "/everywhere", ShowInMobile = true });

        var web = await service.GetMenuAsync("Web", "Admin");
        Assert.Contains(web, n => n.Id == webOnly);
        Assert.Contains(web, n => n.Id == both);

        // An admin holding a phone is still holding a phone.
        var mobile = await service.GetMenuAsync("Mobile", "Admin");
        Assert.DoesNotContain(mobile, n => n.Id == webOnly);
        Assert.Contains(mobile, n => n.Id == both);
    }

    [Fact]
    public async Task Mobile_menu_keeps_a_desktop_only_group_that_holds_a_mobile_form()
    {
        using var harness = new TestHarness();
        var service = NewService(harness);

        // Containers are pure navigation, so their own flag is not consulted — otherwise a
        // mobile-published form would be stranded under a group nobody ticked.
        var group = await service.CreateAsync(new CreateMenuModuleDto { Name = "Masters Zone", ShowInMobile = false });
        var child = await service.CreateAsync(new CreateMenuModuleDto { Name = "Festivals Zone", RoutePath = "/festivals-zone", ShowInMobile = true, ParentId = group });
        var hidden = await service.CreateAsync(new CreateMenuModuleDto { Name = "Roles Zone", RoutePath = "/roles-zone", ShowInMobile = false, ParentId = group });

        var mobile = await service.GetMenuAsync("Mobile", "Admin");
        var node = Assert.Single(mobile, n => n.Id == group);
        Assert.Equal(child, Assert.Single(node.Children).Id);
        Assert.DoesNotContain(node.Children, c => c.Id == hidden);
    }

    [Fact]
    public async Task Mobile_group_disappears_once_its_last_mobile_form_does()
    {
        using var harness = new TestHarness();
        var service = NewService(harness);

        var group = await service.CreateAsync(new CreateMenuModuleDto { Name = "Empty Zone", ShowInMobile = true });
        await service.CreateAsync(new CreateMenuModuleDto { Name = "Desk Form", RoutePath = "/desk-form", ShowInMobile = false, ParentId = group });

        var mobile = await service.GetMenuAsync("Mobile", "Admin");
        Assert.DoesNotContain(mobile, n => n.Id == group);
    }

    [Fact]
    public async Task Web_menu_ignores_the_mobile_flag_entirely()
    {
        using var harness = new TestHarness();
        var service = NewService(harness);

        var id = await service.CreateAsync(new CreateMenuModuleDto { Name = "Desk Master", RoutePath = "/desk-master", ShowInMobile = false });

        // Null platform means Web, which is what an older client that sends no platform gets.
        Assert.Contains(await service.GetMenuAsync("Web", "Admin"), n => n.Id == id);
        Assert.Contains(await service.GetMenuAsync(null, "Admin"), n => n.Id == id);
    }

    [Fact]
    public async Task Update_changes_fields()
    {
        using var harness = new TestHarness();
        var service = NewService(harness);

        var id = await service.CreateAsync(new CreateMenuModuleDto { Name = "Temples", Icon = "🛕", DisplayOrder = 5 });
        await service.UpdateAsync(id, new UpdateMenuModuleDto { Name = "Temples & Yatra", Icon = "🛕", RoutePath = "/temples", DisplayOrder = 3, ShowInMobile = false });

        var dto = await service.GetByIdAsync(id);
        Assert.NotNull(dto);
        Assert.Equal("Temples & Yatra", dto!.Name);
        Assert.Equal("/temples", dto.RoutePath);
        Assert.False(dto.ShowInMobile);
    }
}
