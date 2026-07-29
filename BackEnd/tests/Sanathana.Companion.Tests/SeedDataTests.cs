using Microsoft.EntityFrameworkCore;

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
}
