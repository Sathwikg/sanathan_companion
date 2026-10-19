using App.Core.Services;

namespace App.Tests;

public class AppClockTests
{
    [Fact]
    public void Ist_is_utc_plus_five_and_a_half_hours_with_an_unspecified_kind()
    {
        var ist = AppClock.ToIst(new DateTime(2026, 10, 15, 9, 30, 0, DateTimeKind.Utc));
        Assert.Equal(new DateTime(2026, 10, 15, 15, 0, 0), ist);
        Assert.Equal(DateTimeKind.Unspecified, ist.Kind);
    }

    [Fact]
    public void The_date_rolls_at_ist_midnight_not_utc_midnight()
        => Assert.Equal(new DateOnly(2026, 10, 16), DateOnly.FromDateTime(AppClock.ToIst(new DateTime(2026, 10, 15, 19, 0, 0, DateTimeKind.Utc))));

    [Fact]
    public void A_timer_is_never_armed_for_less_than_a_second()
    {
        var now = new DateTime(2026, 10, 15, 15, 5, 0);
        Assert.Equal(TimeSpan.FromSeconds(1), AppClock.Until(now, now));
        Assert.Equal(TimeSpan.FromMinutes(5), AppClock.Until(now, now.AddMinutes(5)));
    }
}