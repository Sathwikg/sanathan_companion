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
