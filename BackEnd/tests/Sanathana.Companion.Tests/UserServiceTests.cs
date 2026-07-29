using Sanathana.Companion.Application.Services;
using Sanathana.Companion.Domain.Entities;
using Sanathana.Companion.Domain.Exceptions;
using Sanathana.Companion.Infrastructure.Seed;

namespace Sanathana.Companion.Tests;

public class UserServiceTests
{
    [Fact]
    public async Task MyProfile_returns_details_streak_and_day_grouped_timeline()
    {
        using var harness = new TestHarness();
        var ctx = harness.Context;
        var userId = SeedConstants.AdminUserId; // seeded admin; no sadhana is seeded
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5.5));

        ctx.SadhanaLogs.AddRange(
            new SadhanaLog
            {
                Id = Guid.NewGuid(), UserId = userId, Date = today, ChantConfigId = Guid.NewGuid(),
                ChantName = "Vishnu Sahasranama", DeityName = "Vishnu", CategoryName = "Sahasranama",
                TargetCount = 108, TotalCount = 216, MalasCompleted = 2
            },
            new SadhanaLog
            {
                Id = Guid.NewGuid(), UserId = userId, Date = today, ChantConfigId = Guid.NewGuid(),
                ChantName = "Om Namah Shivaya", DeityName = "Shiva", CategoryName = "Mantra",
                TargetCount = 108, TotalCount = 108, MalasCompleted = 1
            },
            new SadhanaLog
            {
                Id = Guid.NewGuid(), UserId = userId, Date = today.AddDays(-1), ChantConfigId = Guid.NewGuid(),
                ChantName = "Hanuman Chalisa", DeityName = "Hanuman", CategoryName = "Chalisa",
                TargetCount = 40, TotalCount = 40, MalasCompleted = 1
            });
        ctx.SadhanaStreaks.Add(new SadhanaStreak
        {
            Id = Guid.NewGuid(), UserId = userId,
            CurrentStreak = 3, LongestStreak = 5, TotalMalas = 42, TotalDaysPracticed = 10, LastPracticeDate = today
        });
        await ctx.SaveChangesAsync();

        var service = new UserService(harness.UnitOfWork);
        var profile = await service.GetMyProfileAsync(userId);

        Assert.NotNull(profile);
        Assert.Equal("Admin", profile!.RoleName);
        Assert.Equal(3, profile.CurrentStreak);
        Assert.Equal(5, profile.LongestStreak);
        Assert.Equal(42, profile.TotalMalas);
        Assert.True(profile.PracticedToday);

        // Timeline is grouped by day, most recent first.
        Assert.Equal(2, profile.Timeline.Count);
        var todayDay = profile.Timeline[0];
        Assert.Equal(today, todayDay.Date);
        Assert.Equal(2, todayDay.Entries.Count);
        Assert.Equal(3, todayDay.TotalMalas);   // 2 + 1
        Assert.Equal(324, todayDay.TotalCount);  // 216 + 108
        Assert.Equal("Vishnu Sahasranama", todayDay.Entries[0].ChantName); // sorted by malas desc
    }

    [Fact]
    public async Task Default_region_can_be_set_cleared_and_is_validated()
    {
        using var harness = new TestHarness();
        var ctx = harness.Context;
        var userId = SeedConstants.AdminUserId;

        var region = new Region { Id = Guid.NewGuid(), Name = "PrefRegion", IsActive = true };
        var retired = new Region { Id = Guid.NewGuid(), Name = "RetiredRegion", IsActive = false };
        ctx.Regions.AddRange(region, retired);
        await ctx.SaveChangesAsync();

        var service = new UserService(harness.UnitOfWork);

        await service.UpdateDefaultRegionAsync(userId, region.Id);
        var profile = await service.GetMyProfileAsync(userId);
        Assert.Equal(region.Id, profile!.DefaultRegionId);
        Assert.Equal("PrefRegion", profile.DefaultRegionName);

        // an administrator may clear it (they can view all regions)
        await service.UpdateDefaultRegionAsync(userId, null);
        profile = await service.GetMyProfileAsync(userId);
        Assert.Null(profile!.DefaultRegionId);

        // an inactive or unknown region is rejected
        await Assert.ThrowsAsync<BadRequestException>(() => service.UpdateDefaultRegionAsync(userId, retired.Id));
        await Assert.ThrowsAsync<BadRequestException>(() => service.UpdateDefaultRegionAsync(userId, Guid.NewGuid()));
    }

    [Fact]
    public async Task A_seeker_cannot_clear_their_region_but_an_admin_can()
    {
        using var harness = new TestHarness();
        var ctx = harness.Context;

        var region = new Region { Id = Guid.NewGuid(), Name = "SeekerRegion", IsActive = true };
        ctx.Regions.Add(region);
        var seeker = new User
        {
            UserId = Guid.NewGuid(), FullName = "Test Seeker", Email = "seeker-region@test.local",
            MobileNumber = "9990001111", PasswordHash = "x", RoleId = SeedConstants.SanathanRoleId,
            DefaultRegionId = region.Id
        };
        ctx.Users.Add(seeker);
        await ctx.SaveChangesAsync();

        var service = new UserService(harness.UnitOfWork);

        // "All Regions" (null) is administrator-only.
        await Assert.ThrowsAsync<BadRequestException>(() => service.UpdateDefaultRegionAsync(seeker.UserId, null));

        // ...but a seeker can still switch to another valid region.
        var other = new Region { Id = Guid.NewGuid(), Name = "OtherRegion", IsActive = true };
        ctx.Regions.Add(other);
        await ctx.SaveChangesAsync();

        await service.UpdateDefaultRegionAsync(seeker.UserId, other.Id);
        var profile = await service.GetMyProfileAsync(seeker.UserId);
        Assert.Equal(other.Id, profile!.DefaultRegionId);
    }
}
