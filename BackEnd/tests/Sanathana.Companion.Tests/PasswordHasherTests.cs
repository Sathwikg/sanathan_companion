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
    public void Seeded_admin_hash_verifies_nothing()
    {
        // This test used to assert the opposite — that the seeded hash accepted "admin". That was
        // the vulnerability: a working administrator credential in every database, published in
        // the README of a public repository. The account now ships locked and is opened from
        // Admin__InitialPassword. See SeededAdminTests for the full set.
        var hasher = new BCryptPasswordHasher();
        Assert.False(hasher.Verify("admin", SeedConstants.AdminPasswordHash));
    }
}
