using Sanathana.Companion.Application.DTOs.Sadhana;
using Sanathana.Companion.Application.Interfaces;
using Sanathana.Companion.Application.Services;
using Sanathana.Companion.Domain.Entities;
using Sanathana.Companion.Domain.Exceptions;

namespace Sanathana.Companion.Tests;

/// <summary>
/// A seeker's own mala size and vibration choice per chant, and how malas are counted once the
/// size can change in the middle of a day.
/// </summary>
public class JapaSettingsTests
{
    private sealed class Seeker : ICurrentUserService
    {
        public Guid? UserId { get; } = Guid.NewGuid();
        public string? Email => null;
        public bool IsAuthenticated => true;
    }

    /// <summary>A chant whose category configures a 108-count mala.</summary>
    private static async Task<(TestHarness Harness, SadhanaService Service, Seeker Me, Guid ChantId)> ArrangeAsync()
    {
        var harness = new TestHarness();
        var ctx = harness.Context;
        var category = new Chant { Id = Guid.NewGuid(), Name = "JapaTestCategory", HasCount = true, Count = 108, IsActive = true };
        ctx.Chants.Add(category);
        var chant = new ChantConfig { Id = Guid.NewGuid(), ChantId = category.Id, Name = "JapaTestMantra", ChantText = "<p>om</p>", IsActive = true };
        ctx.ChantConfigs.Add(chant);
        await ctx.SaveChangesAsync();

        var me = new Seeker();
        return (harness, new SadhanaService(harness.UnitOfWork, me), me, chant.Id);
    }

    private static Task<LogCountResultDto> Count(SadhanaService s, Guid chant, int total)
        => s.LogCountAsync(new LogCountDto { ChantConfigId = chant, TotalCount = total });

    [Fact]
    public async Task A_chant_starts_on_its_configured_count_with_vibration_on()
    {
        var (harness, service, _, chant) = await ArrangeAsync();
        using var _h = harness;

        var detail = await service.GetChantAsync(chant);

        Assert.Equal(108, detail!.TargetCount);
        Assert.Equal(108, detail.DefaultTargetCount);
        Assert.Null(detail.CustomTargetCount);
        Assert.True(detail.VibrationEnabled);
        Assert.Equal(0, detail.CountInMala);
    }

    [Fact]
    public async Task The_seekers_own_count_makes_their_mala()
    {
        var (harness, service, _, chant) = await ArrangeAsync();
        using var _h = harness;

        var saved = await service.SaveChantSettingsAsync(chant, new SaveChantSettingsDto { MalaSize = 11, VibrationEnabled = true });
        Assert.Equal(11, saved.TargetCount);
        Assert.Equal(11, saved.CustomTargetCount);
        Assert.Equal(108, saved.DefaultTargetCount);

        var result = await Count(service, chant, 11);

        Assert.True(result.MalaCompleted);
        Assert.Equal(1, result.Session.MalasCompleted);
        Assert.Equal(0, result.Session.CountInMala);
        Assert.Equal(1, result.Streak.TotalMalas);
        Assert.True(result.StreakSecuredToday);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(100_001)]
    public async Task A_mala_needs_at_least_three_chants(int size)
    {
        var (harness, service, _, chant) = await ArrangeAsync();
        using var _h = harness;

        await Assert.ThrowsAsync<BadRequestException>(() =>
            service.SaveChantSettingsAsync(chant, new SaveChantSettingsDto { MalaSize = size }));
    }

    [Fact]
    public async Task Three_is_allowed_and_clearing_it_returns_to_the_configured_count()
    {
        var (harness, service, _, chant) = await ArrangeAsync();
        using var _h = harness;

        Assert.Equal(3, (await service.SaveChantSettingsAsync(chant, new SaveChantSettingsDto { MalaSize = 3 })).TargetCount);

        var back = await service.SaveChantSettingsAsync(chant, new SaveChantSettingsDto { MalaSize = null });
        Assert.Equal(108, back.TargetCount);
        Assert.Null(back.CustomTargetCount);
    }

    [Fact]
    public async Task Changing_the_size_mid_day_neither_invents_nor_takes_back_malas()
    {
        var (harness, service, _, chant) = await ArrangeAsync();
        using var _h = harness;

        // One mala at 108, then seven more chants.
        await Count(service, chant, 115);
        Assert.Equal(1, (await service.GetChantAsync(chant))!.TodayMalas);

        // Re-dividing 115 by 11 would have made ten malas. Only the seven in progress carry over.
        var after = await service.SaveChantSettingsAsync(chant, new SaveChantSettingsDto { MalaSize = 11 });
        Assert.Equal(1, after.TodayMalas);
        Assert.Equal(7, after.CountInMala);

        var next = await Count(service, chant, 118);
        Assert.Equal(1, next.Session.MalasCompleted);
        Assert.Equal(10, next.Session.CountInMala);

        var done = await Count(service, chant, 119);
        Assert.True(done.MalaCompleted);
        Assert.Equal(2, done.Session.MalasCompleted);
        Assert.Equal(0, done.Session.CountInMala);
        Assert.Equal(2, done.Streak.TotalMalas);

        // And growing the size back does not take the second mala away.
        var bigger = await service.SaveChantSettingsAsync(chant, new SaveChantSettingsDto { MalaSize = 108 });
        Assert.Equal(2, bigger.TodayMalas);
    }

    [Fact]
    public async Task Reset_clears_only_the_mala_in_progress()
    {
        var (harness, service, _, chant) = await ArrangeAsync();
        using var _h = harness;

        await Count(service, chant, 115);           // 1 mala + 7

        var reset = await Count(service, chant, 108);
        Assert.Equal(1, reset.Session.MalasCompleted);
        Assert.Equal(0, reset.Session.CountInMala);

        // A total below the banked malas cannot unbank one.
        var lower = await Count(service, chant, 50);
        Assert.Equal(108, lower.Session.TotalCount);
        Assert.Equal(1, lower.Session.MalasCompleted);
    }

    [Fact]
    public async Task Vibration_off_is_remembered_for_that_chant()
    {
        var (harness, service, _, chant) = await ArrangeAsync();
        using var _h = harness;

        await service.SaveChantSettingsAsync(chant, new SaveChantSettingsDto { MalaSize = null, VibrationEnabled = false });

        Assert.False((await service.GetChantAsync(chant))!.VibrationEnabled);
    }

    [Fact]
    public async Task The_lists_show_the_seekers_own_mala_size()
    {
        var (harness, service, _, chant) = await ArrangeAsync();
        using var _h = harness;

        await service.SaveChantSettingsAsync(chant, new SaveChantSettingsDto { MalaSize = 27 });

        var listed = Assert.Single(await service.GetChantsAsync("JapaTestMantra"));
        Assert.Equal(27, listed.TargetCount);
    }

    [Fact]
    public async Task Another_seekers_settings_are_their_own()
    {
        var (harness, service, _, chant) = await ArrangeAsync();
        using var _h = harness;
        await service.SaveChantSettingsAsync(chant, new SaveChantSettingsDto { MalaSize = 21, VibrationEnabled = false });

        var other = new SadhanaService(harness.UnitOfWork, new Seeker());
        var theirs = await other.GetChantAsync(chant);

        Assert.Equal(108, theirs!.TargetCount);
        Assert.True(theirs.VibrationEnabled);
    }

    [Fact]
    public async Task Closing_the_account_erases_the_settings()
    {
        var (harness, service, me, chant) = await ArrangeAsync();
        using var _h = harness;
        await service.SaveChantSettingsAsync(chant, new SaveChantSettingsDto { MalaSize = 21 });

        await harness.UnitOfWork.Users.PurgeUserDataAsync(me.UserId!.Value);
        await harness.UnitOfWork.SaveChangesAsync();

        Assert.Empty(harness.Context.UserChantSettings.Where(s => s.UserId == me.UserId));
    }
}
