using Sanathana.Companion.Application.Services;
using Sanathana.Companion.Domain.Entities;
using Sanathana.Companion.Infrastructure.Persistence;
using Sanathana.Companion.Infrastructure.Seed;

namespace Sanathana.Companion.Tests;

/// <summary>
/// Account deletion and data export. Both Apple and Google require an app that lets people create
/// an account to let them delete it, and deleting must actually erase what was collected — a
/// half-purge that leaves practice history behind is worse than none, because it looks done.
/// </summary>
public class AccountLifecycleTests
{
    private static UserService NewService(TestHarness harness)
        => new(harness.UnitOfWork, harness.Hasher, new AccountDataReader(harness.Context));

    /// <summary>A user with one row in every table that references them.</summary>
    private static async Task<Guid> SeedUserWithDataAsync(TestHarness harness, string password)
    {
        var ctx = harness.Context;
        var userId = Guid.NewGuid();

        ctx.Users.Add(new User
        {
            UserId = userId,
            FullName = "Test Seeker",
            Email = "seeker@example.org",
            MobileNumber = "9999999999",
            PasswordHash = harness.Hasher.Hash(password),
            RoleId = SeedConstants.AdminRoleId
        });

        ctx.SadhanaLogs.Add(new SadhanaLog
        {
            Id = Guid.NewGuid(), UserId = userId, Date = DateOnly.FromDateTime(DateTime.UtcNow),
            ChantConfigId = Guid.NewGuid(), ChantName = "Hanuman Chalisa",
            TargetCount = 40, TotalCount = 40, MalasCompleted = 1
        });
        ctx.SadhanaStreaks.Add(new SadhanaStreak { Id = Guid.NewGuid(), UserId = userId, CurrentStreak = 3 });
        ctx.UserFavorites.Add(new UserFavorite { Id = Guid.NewGuid(), UserId = userId, FavoriteType = "Deity", ItemId = Guid.NewGuid() });
        ctx.UserNotificationSettings.Add(new UserNotificationSetting { Id = Guid.NewGuid(), UserId = userId, MasterEnabled = true });
        ctx.UserNotificationPreferences.Add(new UserNotificationPreference { Id = Guid.NewGuid(), UserId = userId, NotificationConfigId = Guid.NewGuid() });
        ctx.Feedbacks.Add(new Feedback { Id = Guid.NewGuid(), UserId = userId, IssueTypeId = SeedConstants.IssueTypeOtherId, Description = "hello", Status = "New" });

        await ctx.SaveChangesAsync();
        return userId;
    }

    [Fact]
    public async Task Deleting_an_account_erases_every_row_that_referenced_the_user()
    {
        using var harness = new TestHarness();
        var userId = await SeedUserWithDataAsync(harness, "a strong passphrase");

        var deleted = await NewService(harness).DeleteMyAccountAsync(userId, "a strong passphrase");

        Assert.True(deleted);

        var ctx = harness.Context;
        Assert.Empty(ctx.Users.Where(u => u.UserId == userId));
        Assert.Empty(ctx.SadhanaLogs.Where(x => x.UserId == userId));
        Assert.Empty(ctx.SadhanaStreaks.Where(x => x.UserId == userId));
        Assert.Empty(ctx.UserFavorites.Where(x => x.UserId == userId));
        Assert.Empty(ctx.UserNotificationSettings.Where(x => x.UserId == userId));
        Assert.Empty(ctx.UserNotificationPreferences.Where(x => x.UserId == userId));
        // Feedback's FK is Restrict, so a cascade delete would have failed outright here.
        Assert.Empty(ctx.Feedbacks.Where(x => x.UserId == userId));
    }

    [Fact]
    public async Task A_wrong_password_deletes_nothing()
    {
        using var harness = new TestHarness();
        var userId = await SeedUserWithDataAsync(harness, "a strong passphrase");

        var deleted = await NewService(harness).DeleteMyAccountAsync(userId, "not the passphrase");

        Assert.False(deleted);
        Assert.NotEmpty(harness.Context.Users.Where(u => u.UserId == userId));
        Assert.NotEmpty(harness.Context.SadhanaLogs.Where(x => x.UserId == userId));
    }

    [Fact]
    public async Task Deleting_one_account_leaves_everybody_else_alone()
    {
        using var harness = new TestHarness();
        var mine = await SeedUserWithDataAsync(harness, "mine");

        var theirs = Guid.NewGuid();
        harness.Context.SadhanaLogs.Add(new SadhanaLog
        {
            Id = Guid.NewGuid(), UserId = theirs, Date = DateOnly.FromDateTime(DateTime.UtcNow),
            ChantConfigId = Guid.NewGuid(), ChantName = "Theirs", TargetCount = 108
        });
        await harness.Context.SaveChangesAsync();

        await NewService(harness).DeleteMyAccountAsync(mine, "mine");

        Assert.NotEmpty(harness.Context.SadhanaLogs.Where(x => x.UserId == theirs));
    }

    [Fact]
    public async Task The_export_carries_the_data_the_app_holds()
    {
        using var harness = new TestHarness();
        var userId = await SeedUserWithDataAsync(harness, "a strong passphrase");

        var export = await NewService(harness).ExportMyDataAsync(userId);

        Assert.NotNull(export);
        Assert.Equal("Test Seeker", export!.Account.FullName);
        Assert.Equal("seeker@example.org", export.Account.Email);
        Assert.Single(export.Sadhana);
        Assert.Equal("Hanuman Chalisa", export.Sadhana[0].Chant);
        Assert.Single(export.Favorites);
        Assert.Single(export.Feedback);
        Assert.Equal("hello", export.Feedback[0].Description);
        Assert.True(export.Notifications.MasterEnabled);
    }

    [Fact]
    public async Task Exporting_an_account_that_is_gone_returns_nothing_rather_than_throwing()
        => Assert.Null(await NewService(new TestHarness()).ExportMyDataAsync(Guid.NewGuid()));
}
