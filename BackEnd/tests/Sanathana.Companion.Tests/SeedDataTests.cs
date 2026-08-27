using Microsoft.EntityFrameworkCore;
using Sanathana.Companion.Application.Common;

namespace Sanathana.Companion.Tests;

public class SeedDataTests
{
    [Fact]
    public async Task Roles_and_admin_user_are_seeded()
    {
        using var harness = new TestHarness();

        var roles = await harness.Context.Roles.OrderBy(r => r.RoleId).ToListAsync();
        Assert.Equal(2, roles.Count);
        Assert.Equal("Admin", roles[0].RoleName);
        Assert.Equal("Sanathan", roles[1].RoleName);

        var admin = await harness.Context.Users.SingleAsync();
        Assert.Equal("admin", admin.Email);
        Assert.Equal(1, admin.RoleId);
    }

    /// <summary>
    /// Every seeded Code must be byte-identical to its <see cref="ModuleCodes"/> constant.
    /// </summary>
    /// <remarks>
    /// AccessRightsCatalog builds the granted set with StringComparer.Ordinal while the role
    /// lookup beside it is OrdinalIgnoreCase, so a row seeded as "MobileDashboard" would look
    /// right in the Modules screen, pass every other test, and then refuse the form to every
    /// non-administrator at run time. Nothing else in the suite compares the two.
    /// </remarks>
    [Fact]
    public async Task Every_seeded_module_code_matches_a_ModuleCodes_constant_exactly()
    {
        using var harness = new TestHarness();

        var declared = typeof(ModuleCodes)
            .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToHashSet(StringComparer.Ordinal);

        var seeded = await harness.Context.MenuModules
            .Where(m => m.Code != null)
            .Select(m => m.Code!)
            .ToListAsync();

        var unknown = seeded.Where(c => !declared.Contains(c)).OrderBy(c => c, StringComparer.Ordinal).ToList();

        Assert.True(unknown.Count == 0,
            "These seeded MenuModule.Code values match no ModuleCodes constant under an ordinal " +
            "comparison, so ModuleAccessFilter would refuse their form to every non-administrator:" +
            Environment.NewLine + string.Join(Environment.NewLine, unknown));
    }

    /// <summary>
    /// The phone lands on "Mobile Home", and the desktop dashboard stays off the phone.
    /// </summary>
    /// <remarks>
    /// Both halves matter and neither is covered elsewhere. If the dashboard row came back to
    /// mobile there would be two home forms in the bottom bar; if the Mobile Home row lost its
    /// DisplayOrder lead it would fall past the fourth tab into the "More" sheet, on the one
    /// screen a seeker reaches without reading. MobileMenu.IsHome cannot pin it either — that
    /// only matches a "/" route — so the ordering is the whole mechanism.
    /// </remarks>
    [Fact]
    public async Task The_mobile_home_is_published_to_mobile_and_the_desktop_dashboard_is_not()
    {
        using var harness = new TestHarness();

        var mobileHome = await harness.Context.MenuModules
            .SingleAsync(m => m.Code == ModuleCodes.MobileDashboard);

        Assert.True(mobileHome.ShowInMobile);
        Assert.True(mobileHome.IsActive);
        Assert.True(mobileHome.IsVisibleInMenu);
        Assert.Equal("/mobile-dashboard", mobileHome.RoutePath);

        // <= 2, not <= 4. MobileMenu.PrimarySlots is 4, but the leaves are gathered depth-first
        // (MobileMenu.Leaves), so an Admin's mobile menu interleaves the Masters container's
        // children ahead of any later top-level row. Driving the real MenuModuleService over the
        // seeded database shows DisplayOrder 1 or 2 keeps Mobile Home in the first tab, while 3
        // pushes it out of the bar entirely and into the "More" sheet:
        //   1 or 2 -> [Mobile Home | Festivals | Deities | Chants]
        //   3 or 4 -> [Festivals | Deities | Chants | Languages]
        Assert.InRange(mobileHome.DisplayOrder, 1, 2);

        var dashboard = await harness.Context.MenuModules
            .SingleAsync(m => m.Code == ModuleCodes.Dashboard);
        Assert.False(dashboard.ShowInMobile);

        // The seeker must actually hold the grant, or the page 403s behind a visible tab.
        var granted = await harness.Context.ModuleRoleMappings
            .SingleAsync(r => r.MenuModuleId == mobileHome.Id);
        Assert.True(granted.MobileEnabled);
    }
}
