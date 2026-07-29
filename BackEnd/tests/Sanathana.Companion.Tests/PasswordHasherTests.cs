using Sanathana.Companion.Infrastructure.Identity;
using Sanathana.Companion.Infrastructure.Seed;

namespace Sanathana.Companion.Tests;

public class PasswordHasherTests
{
    [Fact]
    public void Hash_and_verify_roundtrip()
    {
        var hasher = new BCryptPasswordHasher();
        var hash = hasher.Hash("MyPass123");

        Assert.NotEqual("MyPass123", hash);
        Assert.True(hasher.Verify("MyPass123", hash));
        Assert.False(hasher.Verify("wrong", hash));
    }

    [Fact]
    public void Seeded_admin_hash_verifies_admin_password()
    {
        var hasher = new BCryptPasswordHasher();
        Assert.True(hasher.Verify("admin", SeedConstants.AdminPasswordHash));
    }
}
