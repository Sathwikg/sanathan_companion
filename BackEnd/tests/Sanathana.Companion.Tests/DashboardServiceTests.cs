using Sanathana.Companion.Application.Common.Translation;
using Sanathana.Companion.Application.DTOs.Dashboard;
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

    /// <summary>
    /// The phone home shows prayers and Today's Bhakti beside Telugu labels, so their database text
    /// is annotated — but DeityType and Slot are compared to English literals on the client, and a
    /// translated value would silently change the avatar and the prayer icon. Every value below IS a
    /// dictionary hit, so an unannotated property coming back unchanged proves it was never touched.
    /// </summary>
    [Fact]
    public void Dashboard_display_text_is_translated_but_client_identifiers_stay_english()
    {
        var deityId = Guid.NewGuid();
        var festival = new Dictionary<string, string> { ["Holi"] = "[holi]", ["Vasant Panchami"] = "[vasant]" };
        var other = new Dictionary<string, string>
        {
            ["Sunday"] = "[sunday]", ["Navami"] = "[navami]", ["Hanuman"] = "[hanuman]",
            ["Goddess"] = "[goddess]", ["Stotra"] = "[stotra]", ["Morning"] = "[morning]"
        };
        var all = festival.Concat(other).ToDictionary(kv => kv.Key, kv => kv.Value);

        var snapshot = new TranslationSnapshot(
            Guid.NewGuid(), "te",
            new Dictionary<string, string> { [TranslationSnapshot.EntityKey("Deity", deityId.ToString(), "Name")] = "[row]" },
            all.ToDictionary(kv => TermMatcher.NormaliseKey(kv.Key), kv => kv.Value),
            new Dictionary<string, TermMatcher> { ["festival"] = new(festival), ["panchangam"] = new(other) },
            new TermMatcher(all),
            new HashSet<string>());

        var bhakti = new TodayBhaktiDto
        {
            DayOfWeek = "Sunday",
            // "Rama Navami" is not a festival term: the category scope must stop the panchangam
            // "Navami" biting into it and leaving a half-translated name.
            FestivalName = "Holi, Vasant Panchami, Rama Navami",
            Deities =
            [
                new TodayDeityDto
                {
                    Id = deityId, Name = "Hanuman", DeityType = "Goddess", Days = ["Sunday"], Reason = "Sunday",
                    Sadhanas = [new TodaySadhanaDto { Name = "Stotra", CategoryName = "Stotra" }]
                }
            ]
        };
        var prayers = new PrayersDto
        {
            Prayers = [new PrayerDto { Name = "Stotra", CategoryName = "Stotra", DeityNames = ["Hanuman"], TimeDescription = "Morning", Slot = "Morning" }]
        };

        // A festival reason keeps its English prefix, which the client re-words, and translates the name.
        var festivalDeity = new TodayDeityDto { Id = Guid.NewGuid(), Name = "Hanuman", Reason = "Festival · Holi" };

        new ObjectGraphTranslator(snapshot).Walk(bhakti);
        new ObjectGraphTranslator(snapshot).Walk(prayers);
        new ObjectGraphTranslator(snapshot).Walk(festivalDeity);

        Assert.Equal("[holi], [vasant], Rama Navami", bhakti.FestivalName);
        var deity = Assert.Single(bhakti.Deities);
        Assert.Equal("[row]", deity.Name);                 // per-row override beats the dictionary
        Assert.Equal("[sunday]", Assert.Single(deity.Days));
        Assert.Equal("[sunday]", deity.Reason);
        Assert.Equal("Festival · [holi]", festivalDeity.Reason);
        Assert.Equal("[stotra]", deity.Sadhanas[0].CategoryName);
        var prayer = Assert.Single(prayers.Prayers);
        Assert.Equal("[stotra]", prayer.CategoryName);
        Assert.Equal("[hanuman]", Assert.Single(prayer.DeityNames));

        // Identifiers and deliberately-English text.
        Assert.Equal("Goddess", deity.DeityType);
        Assert.Equal("Morning", prayer.Slot);
        Assert.Equal("Sunday", bhakti.DayOfWeek);
        Assert.Equal("Stotra", deity.Sadhanas[0].Name);
        Assert.Equal("Stotra", prayer.Name);
        Assert.Equal("Morning", prayer.TimeDescription);
    }
}
