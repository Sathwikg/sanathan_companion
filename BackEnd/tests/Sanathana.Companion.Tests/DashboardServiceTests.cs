using Sanathana.Companion.Application.Services;
using Sanathana.Companion.Domain.Entities;

namespace Sanathana.Companion.Tests;

public class DashboardServiceTests
{
    [Fact]
    public async Task Admin_stats_count_the_seeded_admin_and_start_sadhana_at_zero()
    {
        using var harness = new TestHarness();
        var service = new DashboardService(harness.UnitOfWork);

        var stats = await service.GetAdminStatsAsync();

        // The seed creates the default admin user.
        Assert.True(stats.TotalUsers >= 1);
        Assert.True(stats.TotalAdmins >= 1);
        Assert.Equal(stats.TotalUsers - stats.TotalAdmins, stats.TotalSeekers);

        // No sadhana has been logged in a fresh database.
        Assert.Equal(0, stats.TotalMalas);
        Assert.Equal(0, stats.TotalSessions);
        Assert.Equal(0, stats.ActiveToday);
        Assert.Equal(0L, stats.TotalJapa);
        Assert.Equal(0, stats.LongestStreak);
    }

    [Fact]
    public async Task TodayBhakti_surfaces_todays_deity_and_its_configured_sadhana()
    {
        using var harness = new TestHarness();
        var ctx = harness.Context;
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5.5)); // IST, as the service computes it

        // A festival that falls today makes this deterministic regardless of the weekday the test runs on.
        ctx.Festivals.Add(new Festival
        {
            Id = Guid.NewGuid(), Name = "TestBhaktiFest", Year = today.Year, Date = today, IsActive = true
        });
        var deity = new Deity
        {
            Id = Guid.NewGuid(), Name = "TestBhaktiDeity", DeityType = "God",
            Description = "test deity", Festivals = "TestBhaktiFest", IsActive = true
        };
        ctx.Deities.Add(deity);
        var chant = new Chant { Id = Guid.NewGuid(), Name = "TestCategory", HasCount = true, Count = 108, IsActive = true };
        ctx.Chants.Add(chant);
        var cfg = new ChantConfig
        {
            Id = Guid.NewGuid(), ChantId = chant.Id, Name = "TestBhaktiStotram",
            DeityIds = deity.Id.ToString(), ChantText = "<p>om</p>", IsActive = true
        };
        ctx.ChantConfigs.Add(cfg);
        await ctx.SaveChangesAsync();

        var service = new DashboardService(harness.UnitOfWork);
        var result = await service.GetTodayBhaktiAsync();

        Assert.True(result.IsFestivalDay);
        Assert.Contains("TestBhaktiFest", result.FestivalName);

        var mine = result.Deities.Single(d => d.Id == deity.Id);
        Assert.StartsWith("Festival · TestBhaktiFest", mine.Reason);

        var sadhana = Assert.Single(mine.Sadhanas);
        Assert.Equal(cfg.Id, sadhana.ChantConfigId);
        Assert.Equal("TestBhaktiStotram", sadhana.Name);
        Assert.Equal("TestCategory", sadhana.CategoryName);
    }

    [Fact]
    public async Task TodayBhakti_limits_deities_to_the_selected_region()
    {
        using var harness = new TestHarness();
        var ctx = harness.Context;
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5.5));

        var north = new Region { Id = Guid.NewGuid(), Name = "TestNorth", IsActive = true };
        var south = new Region { Id = Guid.NewGuid(), Name = "TestSouth", IsActive = true };
        ctx.Regions.AddRange(north, south);

        // A festival with no regions applies everywhere, so only the deity mapping varies.
        ctx.Festivals.Add(new Festival
        {
            Id = Guid.NewGuid(), Name = "RegionFest", Year = today.Year, Date = today, IsActive = true
        });
        // Deities map to regions by NAME.
        ctx.Deities.AddRange(
            new Deity { Id = Guid.NewGuid(), Name = "NorthOnlyGod", DeityType = "God", Festivals = "RegionFest", Regions = "TestNorth", IsActive = true },
            new Deity { Id = Guid.NewGuid(), Name = "SouthOnlyGod", DeityType = "God", Festivals = "RegionFest", Regions = "TestSouth", IsActive = true },
            new Deity { Id = Guid.NewGuid(), Name = "EverywhereGod", DeityType = "God", Festivals = "RegionFest", Regions = null, IsActive = true });
        await ctx.SaveChangesAsync();

        var service = new DashboardService(harness.UnitOfWork);

        var northView = await service.GetTodayBhaktiAsync(north.Id);
        var names = northView.Deities.Select(d => d.Name).ToList();
        Assert.Contains("NorthOnlyGod", names);
        Assert.Contains("EverywhereGod", names);   // unmapped = shown everywhere
        Assert.DoesNotContain("SouthOnlyGod", names);

        // No region chosen → nothing is filtered out.
        var allView = await service.GetTodayBhaktiAsync(null);
        var allNames = allView.Deities.Select(d => d.Name).ToList();
        Assert.Contains("NorthOnlyGod", allNames);
        Assert.Contains("SouthOnlyGod", allNames);
    }

    [Fact]
    public async Task Prayers_classify_by_slot_flag_active_now_and_exclude_untimed_chants()
    {
        using var harness = new TestHarness();
        var ctx = harness.Context;

        var cat = new Chant { Id = Guid.NewGuid(), Name = "PrayerCat", HasCount = true, Count = 108, IsActive = true };
        ctx.Chants.Add(cat);

        // Window covers the whole day → always active "now"; description drives the Food slot.
        var food = new ChantConfig
        {
            Id = Guid.NewGuid(), ChantId = cat.Id, Name = "TestFoodPrayer", ChantText = "<p>x</p>",
            FromTime = new TimeOnly(0, 0), ToTime = new TimeOnly(23, 59), TimeDescription = "Food Prayer", IsActive = true
        };
        var morning = new ChantConfig
        {
            Id = Guid.NewGuid(), ChantId = cat.Id, Name = "TestMorningPrayer", ChantText = "<p>x</p>",
            FromTime = new TimeOnly(4, 30), ToTime = new TimeOnly(5, 0), TimeDescription = "Morning Prayer", IsActive = true
        };
        // No configured time → not a prayer.
        var untimed = new ChantConfig
        {
            Id = Guid.NewGuid(), ChantId = cat.Id, Name = "NoTime", ChantText = "<p>x</p>", IsActive = true
        };
        ctx.ChantConfigs.AddRange(food, morning, untimed);
        await ctx.SaveChangesAsync();

        var service = new DashboardService(harness.UnitOfWork);
        var result = await service.GetPrayersAsync();

        Assert.DoesNotContain(result.Prayers, p => p.ChantConfigId == untimed.Id);

        var f = result.Prayers.Single(p => p.ChantConfigId == food.Id);
        Assert.Equal("Food", f.Slot);
        Assert.True(f.IsActiveNow); // 00:00–23:59 always includes the current time

        var m = result.Prayers.Single(p => p.ChantConfigId == morning.Id);
        Assert.Equal("Morning", m.Slot);

        // Active prayers rank ahead of inactive ones.
        Assert.True(result.Prayers[0].IsActiveNow);
    }
}
