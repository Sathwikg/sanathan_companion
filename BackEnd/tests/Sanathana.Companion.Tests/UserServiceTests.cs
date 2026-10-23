using Sanathana.Companion.Infrastructure.Persistence;
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

        var service = new UserService(harness.UnitOfWork, harness.Hasher, new AccountDataReader(harness.Context));
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

    private static SadhanaLog Log(Guid userId, DateOnly date, Guid chantId, string chant, string? deity, int count, int malas) => new()
    {
        Id = Guid.NewGuid(), UserId = userId, Date = date, ChantConfigId = chantId,
        ChantName = chant, DeityName = deity, CategoryName = "Mantra",
        TargetCount = 108, TotalCount = count, MalasCompleted = malas
    };

    private static User AddSeeker(ApplicationDbContext ctx, DateTime createdUtc)
    {
        var seeker = new User
        {
            UserId = Guid.NewGuid(), FullName = "Insight Seeker", Email = $"{Guid.NewGuid():N}@test.local",
            MobileNumber = "9990002222", PasswordHash = "x", RoleId = SeedConstants.SanathanRoleId,
            CreatedDate = createdUtc
        };
        ctx.Users.Add(seeker);
        // This overload skips StampAudit, which would otherwise overwrite CreatedDate with now and
        // make every test seeker brand new.
        ctx.SaveChanges(acceptAllChangesOnSuccess: true);
        return seeker;
    }

    [Fact]
    public async Task Admin_profile_carries_practice_insights()
    {
        using var harness = new TestHarness();
        var ctx = harness.Context;
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5.5));
        var seeker = AddSeeker(ctx, DateTime.UtcNow.AddDays(-90));
        var id = seeker.UserId;
        var hanuman = Guid.NewGuid();
        var shiva = Guid.NewGuid();

        ctx.SadhanaLogs.AddRange(
            Log(id, today, hanuman, "Hanuman Mantra", "Hanuman", 216, 2),
            Log(id, today, shiva, "Om Namah Shivaya", "Shiva", 108, 1),
            Log(id, today.AddDays(-1), hanuman, "Hanuman Mantra", "Hanuman", 540, 5),
            Log(id, today.AddDays(-2), shiva, "Om Namah Shivaya", "Shiva", 50, 0),   // counted, no mala
            Log(id, today.AddDays(-3), shiva, "Om Namah Shivaya", "Shiva", 0, 0),    // a Reset: not practice
            Log(id, today.AddDays(-40), hanuman, "Hanuman Mantra", "Hanuman", 108, 1));
        ctx.SadhanaStreaks.Add(new SadhanaStreak
        {
            Id = Guid.NewGuid(), UserId = id,
            CurrentStreak = 2, LongestStreak = 2, TotalMalas = 9, TotalDaysPracticed = 3, LastPracticeDate = today
        });
        await ctx.SaveChangesAsync();

        var service = new UserService(harness.UnitOfWork, harness.Hasher, new AccountDataReader(harness.Context));
        var profile = await service.GetProfileAsync(id);

        Assert.NotNull(profile);
        Assert.True(profile!.IsActive);
        Assert.Equal(0, profile.DaysSinceLastPractice);

        var i = profile.Insights;
        Assert.Equal(today, i.Today);
        Assert.Equal(216 + 108 + 540 + 50 + 108, i.TotalJapa);
        Assert.Equal(2, i.ChantsPracticed);
        Assert.Equal(today.AddDays(-40), i.FirstPracticeDate);
        Assert.Equal(3.0, i.AverageMalasPerDay);          // 9 malas over 3 days, from the streak

        // Only days with a whole mala are practised; the 50-count day and the Reset are not.
        Assert.Equal(30, i.WindowDays);
        Assert.Equal(2, i.ActiveDays);
        Assert.Equal(8, i.MalasLast30Days);               // 2 + 1 + 5
        Assert.Equal(1, i.MalasPrevious30Days);           // day -40
        Assert.Equal(today.AddDays(-1), i.BestDayDate);
        Assert.Equal(5, i.BestDayMalas);

        // The calendar starts on a Monday and covers the counted-but-incomplete day too.
        Assert.Equal(DayOfWeek.Monday, i.ActivityFrom.DayOfWeek);
        Assert.Equal(4, i.Activity.Count);
        Assert.DoesNotContain(i.Activity, a => a.Date == today.AddDays(-3));

        Assert.Equal(7, i.Weekdays.Count);
        Assert.Equal(8 + 1, i.Weekdays.Sum(w => w.Malas));
        Assert.Equal(3, i.Weekdays.Sum(w => w.Days));

        Assert.Equal("Hanuman Mantra", i.TopChants[0].ChantName);
        Assert.Equal(8, i.TopChants[0].Malas);
        Assert.Equal(3, i.TopChants[0].Days);
        Assert.Equal("Hanuman", i.TopDeities[0].DeityName);
        Assert.Equal(2, i.TopDeities.Count);
    }

    [Fact]
    public async Task A_new_seeker_is_measured_only_over_the_days_since_joining()
    {
        using var harness = new TestHarness();
        var seeker = AddSeeker(harness.Context, DateTime.UtcNow.AddDays(-4));

        var service = new UserService(harness.UnitOfWork, harness.Hasher, new AccountDataReader(harness.Context));
        var profile = await service.GetProfileAsync(seeker.UserId);

        Assert.Equal(5, profile!.Insights.WindowDays);
        Assert.Null(profile.DaysSinceLastPractice);
        Assert.Empty(profile.Insights.Activity);
        Assert.Null(profile.Insights.BestDayDate);
    }

    [Fact]
    public async Task Sadhana_history_is_paged_by_practice_day_newest_first()
    {
        using var harness = new TestHarness();
        var ctx = harness.Context;
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5.5));
        var seeker = AddSeeker(ctx, DateTime.UtcNow.AddDays(-60));
        var id = seeker.UserId;
        var chantA = Guid.NewGuid();
        var chantB = Guid.NewGuid();

        // Ten practice days, every other day, with two chants on the newest one.
        for (var d = 0; d < 10; d++)
            ctx.SadhanaLogs.Add(Log(id, today.AddDays(-2 * d), chantA, "Chant A", "Rama", 108, 1));
        ctx.SadhanaLogs.Add(Log(id, today, chantB, "Chant B", "Sita", 216, 2));
        ctx.SadhanaLogs.Add(Log(id, today.AddDays(-1), chantB, "Chant B", "Sita", 0, 0)); // a Reset
        await ctx.SaveChangesAsync();

        var service = new UserService(harness.UnitOfWork, harness.Hasher, new AccountDataReader(harness.Context));

        var first = await service.GetSadhanaPageAsync(id, 1, 4);
        Assert.Equal(10, first!.TotalCount);
        Assert.Equal(4, first.Items.Count);
        Assert.Equal(today, first.Items[0].Date);
        Assert.Equal(2, first.Items[0].Entries.Count);
        Assert.Equal(3, first.Items[0].TotalMalas);
        Assert.Equal("Chant B", first.Items[0].Entries[0].ChantName);   // most malas first
        Assert.Equal(today.AddDays(-6), first.Items[3].Date);            // the Reset day never shows

        var last = await service.GetSadhanaPageAsync(id, 3, 4);
        Assert.Equal(2, last!.Items.Count);
        Assert.Equal(today.AddDays(-18), last.Items[^1].Date);

        // Out of range pages clamp rather than come back empty.
        var beyond = await service.GetSadhanaPageAsync(id, 99, 4);
        Assert.Equal(3, beyond!.Page);

        Assert.Null(await service.GetSadhanaPageAsync(Guid.NewGuid(), 1, 4));
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

        var service = new UserService(harness.UnitOfWork, harness.Hasher, new AccountDataReader(harness.Context));

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

        var service = new UserService(harness.UnitOfWork, harness.Hasher, new AccountDataReader(harness.Context));

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
