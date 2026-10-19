using App.Core.Models;

namespace App.Core.Services;

/// <summary>The streak as the server would report it on <c>today</c>, re-derived on the client so a payload held across the IST rollover never shows yesterday's "Practiced Today".</summary>
public sealed record EffectiveStreak(int CurrentStreak, bool PracticedToday, bool AtRisk, DateOnly? LastPracticeDate);

/// <summary>One cell of the 7-day strip.</summary>
public readonly record struct StripDay(DateOnly Date, bool Practiced, bool IsToday);

/// <summary>Progress from the last milestone reached to the next; Target is null past the last one.</summary>
public sealed record Milestone(int Floor, int? Target, int Remaining, double Progress)
{
    public bool AllReached => Target is null;
}

/// <summary>
/// What the phone home can say about a streak from the six fields the API sends. No per-day history
/// exists in the payload, so the strip lights exactly the streak's own days and never claims a day
/// was missed. Pure and clock-free.
/// </summary>
public static class StreakInsights
{
    /// <summary>7 days, then 21, a 48-day mandala, 108 (a mala), and onward.</summary>
    public static readonly int[] Milestones = { 7, 21, 48, 108, 216, 365, 1008 };

    /// <summary>
    /// The same rule the server applies when it builds the DTO: the streak is alive only while the
    /// last practice was today or yesterday; "practiced today" means a full mala today.
    /// </summary>
    public static EffectiveStreak Effective(SadhanaStreak streak, DateOnly today)
    {
        var last = streak.LastPracticeDate;
        var alive = last == today || last == today.AddDays(-1);
        var current = alive ? streak.CurrentStreak : 0;
        var practiced = last == today;
        return new EffectiveStreak(current, practiced, current > 0 && !practiced, last);
    }

    /// <summary>Oldest first, ending today: lit for the streak's days, which end at the last practice date.</summary>
    public static IReadOnlyList<StripDay> WeekStrip(SadhanaStreak streak, DateOnly today)
    {
        var eff = Effective(streak, today);
        var days = new List<StripDay>(7);
        for (var i = 6; i >= 0; i--)
        {
            var d = today.AddDays(-i);
            var lit = eff.CurrentStreak > 0 && eff.LastPracticeDate is { } last
                && d <= last && d > last.AddDays(-eff.CurrentStreak);
            days.Add(new StripDay(d, lit, d == today));
        }
        return days;
    }

    public static Milestone NextMilestone(int currentStreak)
    {
        var floor = 0;
        foreach (var m in Milestones)
        {
            if (currentStreak >= m)
            {
                floor = m;
                continue;
            }
            var progress = (double)(currentStreak - floor) / (m - floor);
            return new Milestone(floor, m, m - currentStreak, Math.Clamp(progress, 0, 1));
        }
        return new Milestone(floor, null, 0, 1);
    }
}