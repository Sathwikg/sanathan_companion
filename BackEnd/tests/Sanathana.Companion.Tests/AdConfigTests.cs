using Microsoft.EntityFrameworkCore;
using Sanathana.Companion.Application.DTOs.Ads;
using Sanathana.Companion.Application.Services;
using Sanathana.Companion.Domain.Exceptions;
using Sanathana.Companion.Infrastructure.Seed;

namespace Sanathana.Companion.Tests;

/// <summary>
/// The ad configuration: the format master, the one-format-per-form rule, and how a placement
/// resolves for a given platform.
/// </summary>
public class AdConfigTests
{
    private static AdConfigService Service(TestHarness harness) => new(harness.UnitOfWork);

    /// <summary>A seeded form to hang a placement on. Sadhana is a real navigable module.</summary>
    private static readonly Guid SomeForm = SeedConstants.TodaysSadhanaMenuId;

    private static SaveAdConfigDto Config(params SaveAdModuleDto[] modules) => new()
    {
        AdsEnabled = true,
        UseTestAds = false,
        AndroidAppId = "ca-app-pub-1111111111111111~1111111111",
        IosAppId = "ca-app-pub-2222222222222222~2222222222",
        Modules = modules.ToList()
    };

    // ---------------------------------------------------------------- the master

    [Fact]
    public async Task The_six_formats_google_serves_are_seeded()
    {
        using var harness = new TestHarness();

        var config = await Service(harness).GetConfigAsync();

        Assert.Equal(
            new[] { "banner", "interstitial", "native", "rewarded", "rewardedInterstitial", "appOpen" },
            config.Formats.Select(f => f.Code));
    }

    [Fact]
    public async Task Every_format_carries_the_placement_rule_that_governs_it()
    {
        // The guidance is the reason the master exists rather than an enum: somebody choosing
        // Interstitial should read "not while a seeker is concentrating" at the moment they choose it.
        using var harness = new TestHarness();

        var config = await Service(harness).GetConfigAsync();

        Assert.All(config.Formats, f => Assert.False(string.IsNullOrWhiteSpace(f.PlacementGuidance)));
    }

    [Fact]
    public async Task The_full_screen_formats_are_marked_as_such()
    {
        using var harness = new TestHarness();

        var config = await Service(harness).GetConfigAsync();
        var fullScreen = config.Formats.Where(f => f.IsFullScreen).Select(f => f.Code).ToList();

        Assert.Equal(new[] { "interstitial", "rewarded", "rewardedInterstitial", "appOpen" }, fullScreen);
        Assert.Contains(config.Formats, f => f.Code == "rewarded" && f.RequiresUserOptIn);
    }

    [Fact]
    public async Task Ads_are_off_and_on_test_units_until_somebody_says_otherwise()
    {
        // Nobody should find out this feature exists because real ads appeared in front of seekers.
        using var harness = new TestHarness();

        var config = await Service(harness).GetConfigAsync();

        Assert.False(config.AdsEnabled);
        Assert.True(config.UseTestAds);
    }

    [Fact]
    public async Task Only_forms_are_offered_never_the_containers()
    {
        // A container has no screen, so there is nowhere to put an ad.
        using var harness = new TestHarness();

        var config = await Service(harness).GetConfigAsync();

        Assert.Contains(config.Modules, m => m.MenuModuleId == SomeForm);
        Assert.DoesNotContain(config.Modules, m => m.MenuModuleId == SeedConstants.ConfigurationModuleId);
    }

    // ---------------------------------------------------------------- one format, not several

    [Fact]
    public async Task A_form_that_is_switched_on_must_name_a_format()
    {
        using var harness = new TestHarness();

        var ex = await Assert.ThrowsAsync<BadRequestException>(() => Service(harness).SaveConfigAsync(
            Config(new SaveAdModuleDto { MenuModuleId = SomeForm, IsEnabled = true, AdFormatId = null })));

        Assert.Contains("format", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_format_that_does_not_exist_is_refused()
    {
        using var harness = new TestHarness();

        await Assert.ThrowsAsync<BadRequestException>(() => Service(harness).SaveConfigAsync(
            Config(new SaveAdModuleDto { MenuModuleId = SomeForm, IsEnabled = true, AdFormatId = Guid.NewGuid() })));
    }

    [Fact]
    public async Task Switching_a_form_off_clears_the_format_it_had()
    {
        // Otherwise re-enabling months later silently resurrects a choice nobody remembers making.
        using var harness = new TestHarness();
        var service = Service(harness);

        await service.SaveConfigAsync(Config(new SaveAdModuleDto
        {
            MenuModuleId = SomeForm,
            IsEnabled = true,
            AdFormatId = SeedConstants.AdFormatBannerId,
            AndroidAdUnitId = "ca-app-pub-1111111111111111/1111111111"
        }));

        await service.SaveConfigAsync(Config(new SaveAdModuleDto { MenuModuleId = SomeForm, IsEnabled = false }));

        var row = await harness.Context.AdPlacements.SingleAsync(p => p.MenuModuleId == SomeForm);
        Assert.False(row.IsEnabled);
        Assert.Null(row.AdFormatId);
    }

    [Fact]
    public async Task A_form_can_only_ever_hold_one_placement()
    {
        // The "one type at a time" rule is a property of the shape: a single nullable foreign key,
        // on a row that is unique per module. Saving twice updates rather than accumulates.
        using var harness = new TestHarness();
        var service = Service(harness);

        await service.SaveConfigAsync(Config(new SaveAdModuleDto
        {
            MenuModuleId = SomeForm, IsEnabled = true, AdFormatId = SeedConstants.AdFormatBannerId
        }));
        await service.SaveConfigAsync(Config(new SaveAdModuleDto
        {
            MenuModuleId = SomeForm, IsEnabled = true, AdFormatId = SeedConstants.AdFormatNativeId
        }));

        var rows = await harness.Context.AdPlacements.Where(p => p.MenuModuleId == SomeForm).ToListAsync();
        Assert.Single(rows);
        Assert.Equal(SeedConstants.AdFormatNativeId, rows[0].AdFormatId);
    }

    [Fact]
    public async Task A_form_left_off_stores_nothing_at_all()
    {
        using var harness = new TestHarness();

        await Service(harness).SaveConfigAsync(Config(new SaveAdModuleDto { MenuModuleId = SomeForm, IsEnabled = false }));

        Assert.Empty(await harness.Context.AdPlacements.ToListAsync());
    }

    // ---------------------------------------------------------------- what a client is told

    private static async Task<AdConfigService> ConfiguredAsync(TestHarness harness, bool useTestAds)
    {
        var service = Service(harness);
        await service.SaveConfigAsync(new SaveAdConfigDto
        {
            AdsEnabled = true,
            UseTestAds = useTestAds,
            AndroidAppId = "ca-app-pub-1111111111111111~1111111111",
            IosAppId = "ca-app-pub-2222222222222222~2222222222",
            Modules =
            {
                new SaveAdModuleDto
                {
                    MenuModuleId = SomeForm,
                    IsEnabled = true,
                    AdFormatId = SeedConstants.AdFormatBannerId,
                    AndroidAdUnitId = "ca-app-pub-1111111111111111/1111111111",
                    IosAdUnitId = "ca-app-pub-2222222222222222/2222222222"
                }
            }
        });
        return service;
    }

    [Fact]
    public async Task A_configured_form_resolves_to_that_platform_s_unit()
    {
        using var harness = new TestHarness();
        var service = await ConfiguredAsync(harness, useTestAds: false);

        var android = await service.GetSlotAsync(SomeForm, "Android");
        var ios = await service.GetSlotAsync(SomeForm, "iOS");

        Assert.True(android.ShowAd);
        Assert.Equal("banner", android.FormatCode);
        Assert.Equal("ca-app-pub-1111111111111111/1111111111", android.AdUnitId);
        Assert.Equal("ca-app-pub-2222222222222222/2222222222", ios.AdUnitId);
        Assert.False(android.IsTestAd);
    }

    [Fact]
    public async Task Test_mode_substitutes_googles_own_units_rather_than_the_real_ones()
    {
        // Clicking your own live ads is what gets an AdMob account suspended, so the safe path has
        // to be the one that happens by default while somebody is building a placement.
        using var harness = new TestHarness();
        var service = await ConfiguredAsync(harness, useTestAds: true);

        var slot = await service.GetSlotAsync(SomeForm, "Android");

        Assert.True(slot.IsTestAd);
        Assert.StartsWith("ca-app-pub-3940256099942544/", slot.AdUnitId);
        Assert.Equal("ca-app-pub-3940256099942544~3347511713", slot.AppId);
    }

    [Fact]
    public async Task The_master_switch_beats_every_individual_placement()
    {
        using var harness = new TestHarness();
        var service = await ConfiguredAsync(harness, useTestAds: false);

        await service.SaveConfigAsync(new SaveAdConfigDto
        {
            AdsEnabled = false,
            Modules =
            {
                new SaveAdModuleDto
                {
                    MenuModuleId = SomeForm, IsEnabled = true, AdFormatId = SeedConstants.AdFormatBannerId,
                    AndroidAdUnitId = "ca-app-pub-1111111111111111/1111111111"
                }
            }
        });

        Assert.False((await service.GetSlotAsync(SomeForm, "Android")).ShowAd);
    }

    [Fact]
    public async Task The_web_gets_no_ad_because_this_feature_is_the_mobile_apps()
    {
        using var harness = new TestHarness();
        var service = await ConfiguredAsync(harness, useTestAds: false);

        Assert.False((await service.GetSlotAsync(SomeForm, "Web")).ShowAd);
        Assert.False((await service.GetSlotAsync(SomeForm, null)).ShowAd);
    }

    [Fact]
    public async Task A_form_with_no_unit_for_this_platform_shows_nothing_rather_than_the_other_ones()
    {
        // Handing Android an iOS ad unit is an invalid request to the SDK, not a graceful fallback.
        using var harness = new TestHarness();
        var service = Service(harness);

        await service.SaveConfigAsync(Config(new SaveAdModuleDto
        {
            MenuModuleId = SomeForm,
            IsEnabled = true,
            AdFormatId = SeedConstants.AdFormatBannerId,
            IosAdUnitId = "ca-app-pub-2222222222222222/2222222222"
        }));

        Assert.False((await service.GetSlotAsync(SomeForm, "Android")).ShowAd);
        Assert.True((await service.GetSlotAsync(SomeForm, "iOS")).ShowAd);
    }

    [Fact]
    public async Task A_form_nobody_configured_shows_nothing()
    {
        using var harness = new TestHarness();
        var service = await ConfiguredAsync(harness, useTestAds: false);

        Assert.False((await service.GetSlotAsync(SeedConstants.FavoritesMenuId, "Android")).ShowAd);
    }
}
