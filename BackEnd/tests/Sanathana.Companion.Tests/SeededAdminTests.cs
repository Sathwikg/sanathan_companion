using Sanathana.Companion.Infrastructure.Identity;
using Sanathana.Companion.Infrastructure.Seed;

namespace Sanathana.Companion.Tests;

/// <summary>
/// The seeded administrator shipped as a working admin/admin login, in a public repository whose
/// README published the credential, on a database that migrates on every start-up. These pin it
/// shut.
/// </summary>
public class SeededAdminTests
{
    private static readonly BCryptPasswordHasher Hasher = new();

    [Theory]
    [InlineData("admin")]
    [InlineData("Admin")]
    [InlineData("admin123")]
    [InlineData("password")]
    [InlineData("")]
    [InlineData("0000000000")]
    public void The_seeded_administrator_password_verifies_against_nothing(string attempt)
        => Assert.False(Hasher.Verify(attempt, SeedConstants.AdminPasswordHash));

    [Fact]
    public void The_seeded_hash_is_still_well_formed_bcrypt()
    {
        // It must PARSE. An empty or malformed hash would make BCrypt throw instead of returning
        // false, turning every administrator sign-in attempt into a 500 — and confirming to the
        // caller that the account exists.
        Assert.StartsWith("$2", SeedConstants.AdminPasswordHash);
        Assert.Equal(60, SeedConstants.AdminPasswordHash.Length);

        // Verify() must reach a real answer rather than throw.
        var exception = Record.Exception(() => Hasher.Verify("anything", SeedConstants.AdminPasswordHash));
        Assert.Null(exception);
    }

    [Fact]
    public void An_unparsable_hash_is_a_failed_check_rather_than_an_exception()
    {
        foreach (var malformed in new[] { "", "   ", "not-a-hash", "$2a$11$too-short" })
            Assert.False(Hasher.Verify("admin", malformed));
    }

    [Fact]
    public void A_real_password_still_round_trips()
    {
        var hash = Hasher.Hash("a real passphrase");

        Assert.True(Hasher.Verify("a real passphrase", hash));
        Assert.False(Hasher.Verify("a real passphras", hash));
    }
}
