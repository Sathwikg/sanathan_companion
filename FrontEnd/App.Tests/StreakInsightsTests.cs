using App.Core.Models;
using App.Core.Services;

namespace App.Tests;

public class StreakInsightsTests
{
    private static readonly DateOnly Today = new(2026, 10, 15);

    private static SadhanaStreak Streak(int current, DateOnly? last) => new()
    {
        CurrentStreak = current, LongestStreak = 21, TotalMalas = 86, TotalDaysPracticed = 47,
        LastPracticeDate = last, PracticedToday = last == Today,
    };

    [Fact]
    public void A_streak_whose_last_practice_was_yesterday_is_alive_and_at_risk()
    {
        var eff = StreakInsights.Effective(Streak(12, Today.AddDays(-1)), Today);
        Assert.Equal(12, eff.CurrentStreak);
        Assert.False(eff.PracticedToday);
        Assert.True(eff.AtRisk);
    }

    [Fact]
    public void A_practised_day_is_not_at_risk()
    {
        var eff = StreakInsights.Effective(Streak(12, Today), Today);
        Assert.True(eff.PracticedToday);
        Assert.False(eff.AtRisk);
    }

    [Fact]
    public void A_payload_held_across_the_rollover_is_re_derived()
    {
        // Fetched yesterday evening, when it said 12 and practiced; today it is 12, at risk.
        var stale = Streak(12, Today.AddDays(-1));
        stale.PracticedToday = true;
        Assert.True(StreakInsights.Effective(stale, Today).AtRisk);
        // Two days without practice: zero, exactly as the server would then report.
        Assert.Equal(0, StreakInsights.Effective(Streak(12, Today.AddDays(-2)), Today).CurrentStreak);
    }

    [Fact]
    public void The_strip_is_seven_days_ending_today()
    {
        var strip = StreakInsights.WeekStrip(Streak(3, Today), Today);
        Assert.Equal(7, strip.Count);
        Assert.Equal(Today.AddDays(-6), strip[0].Date);
        Assert.True(strip[6].IsToday);
    }

    [Fact]
    public void A_streak_of_three_ending_today_lights_the_last_three()
    {
        var lit = StreakInsights.WeekStrip(Streak(3, Today), Today).Select(d => d.Practiced).ToArray();
        Assert.Equal(new[] { false, false, false, false, true, true, true }, lit);
    }

    [Fact]
    public void An_at_risk_streak_leaves_today_dark()
    {
        var lit = StreakInsights.WeekStrip(Streak(3, Today.AddDays(-1)), Today).Select(d => d.Practiced).ToArray();
        Assert.Equal(new[] { false, false, false, true, true, true, false }, lit);
    }

    [Fact]
    public void A_long_streak_lights_every_day()
        => Assert.All(StreakInsights.WeekStrip(Streak(12, Today), Today), d => Assert.True(d.Practiced));

    [Fact]
    public void A_cold_streak_lights_nothing()
    {
        Assert.All(StreakInsights.WeekStrip(Streak(0, Today.AddDays(-5)), Today), d => Assert.False(d.Practiced));
        Assert.All(StreakInsights.WeekStrip(Streak(0, null), Today), d => Assert.False(d.Practiced));
    }

    [Theory]
    [InlineData(0, 0, 7, 7)]
    [InlineData(6, 0, 7, 1)]
    [InlineData(7, 7, 21, 14)]
    [InlineData(12, 7, 21, 9)]
    [InlineData(20, 7, 21, 1)]
    [InlineData(21, 21, 48, 27)]
    [InlineData(107, 48, 108, 1)]
    [InlineData(364, 216, 365, 1)]
    [InlineData(365, 365, 1008, 643)]
    public void The_next_milestone_is_the_first_above_the_streak(int streak, int floor, int target, int remaining)
    {
        var m = StreakInsights.NextMilestone(streak);
        Assert.Equal(floor, m.Floor);
        Assert.Equal(target, m.Target);
        Assert.Equal(remaining, m.Remaining);
    }

    [Fact]
    public void Progress_is_measured_from_the_previous_milestone()
        => Assert.Equal(0.5, StreakInsights.NextMilestone(14).Progress, 3);

    [Theory]
    [InlineData(1008)]
    [InlineData(2000)]
    public void Past_the_last_milestone_everything_is_reached(int streak)
    {
        var m = StreakInsights.NextMilestone(streak);
        Assert.True(m.AllReached);
        Assert.Equal(1.0, m.Progress);
        Assert.Equal(0, m.Remaining);
    }
}